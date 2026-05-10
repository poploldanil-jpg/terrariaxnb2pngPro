// XNBExporterPro - LzxDecompressor.cs
// LZX decompression for XNB files
// Based on MonoGame LzxDecoder (LGPL 2.1 / MS-PL dual licensed)
// Original C implementation by Stuart Caie (libmspack)
// C# port by Ali Scissons

using System;
using System.IO;

namespace XNBExporterPro
{
    /// <summary>
    /// Decompresses LZX-compressed data from XNB files.
    /// XNB uses LZX with window size 16 (64KB window).
    /// Data is stored in blocks with 2-byte or 5-byte headers.
    /// </summary>
    public static class LzxDecompressor
    {
        /// <summary>
        /// Decompress LZX data from an XNB stream
        /// </summary>
        public static byte[] Decompress(BinaryReader reader, int compressedSize, int decompressedSize)
        {
            var decoder = new LzxDecoder(16); // XNB uses window bits = 16
            byte[] result = new byte[decompressedSize];

            long startPos = reader.BaseStream.Position;
            long endPos = startPos + compressedSize;

            int destPos = 0;
            while (destPos < decompressedSize)
            {
                // Read block header
                if (reader.BaseStream.Position >= endPos) break;

                int hi = reader.ReadByte();
                int lo;
                int frameSize;
                int blockSize;

                if (hi == 0xFF)
                {
                    // 5-byte header: frame size + block size
                    hi = reader.ReadByte();
                    lo = reader.ReadByte();
                    frameSize = (hi << 8) | lo;

                    hi = reader.ReadByte();
                    lo = reader.ReadByte();
                    blockSize = (hi << 8) | lo;
                }
                else
                {
                    // 2-byte header: block size only, frame size = 32768
                    lo = reader.ReadByte();
                    blockSize = (hi << 8) | lo;
                    frameSize = 0x8000; // 32KB
                }

                if (blockSize == 0 || frameSize == 0)
                    break;

                if (reader.BaseStream.Position + blockSize > reader.BaseStream.Length)
                    blockSize = (int)(reader.BaseStream.Length - reader.BaseStream.Position);

                byte[] blockData = reader.ReadBytes(blockSize);

                byte[] frameData = decoder.DecompressBlock(blockData, blockSize, frameSize);

                int copyLen = Math.Min(frameSize, decompressedSize - destPos);
                Array.Copy(frameData, 0, result, destPos, copyLen);
                destPos += copyLen;
            }

            return result;
        }
    }

    /// <summary>
    /// LZX decoder implementation
    /// </summary>
    internal class LzxDecoder
    {
        // Constants
        private const int MIN_MATCH = 2;
        private const int MAX_MATCH = 257;
        private const int NUM_CHARS = 256;
        private const int PRETREE_NUM_ELEMENTS = 20;
        private const int ALIGNED_NUM_ELEMENTS = 8;
        private const int NUM_PRIMARY_LENGTHS = 7;
        private const int NUM_SECONDARY_LENGTHS = 249;
        private const int PRETREE_MAXSYMBOLS = PRETREE_NUM_ELEMENTS;
        private const int PRETREE_TABLEBITS = 6;
        private const int MAINTREE_MAXSYMBOLS = NUM_CHARS + 50 * 8;
        private const int MAINTREE_TABLEBITS = 12;
        private const int LENGTH_MAXSYMBOLS = NUM_SECONDARY_LENGTHS + 1;
        private const int LENGTH_TABLEBITS = 12;
        private const int ALIGNED_MAXSYMBOLS = ALIGNED_NUM_ELEMENTS;
        private const int ALIGNED_TABLEBITS = 7;

        private enum BlockType { Invalid = 0, Verbatim = 1, Aligned = 2, Uncompressed = 3 }

        // State
        private readonly byte[] window;
        private readonly uint windowSize;
        private uint windowPos;
        private uint R0, R1, R2;
        private ushort mainElements;
        private bool headerRead;
        private BlockType blockType;
        private int blockRemaining;

        // Huffman tables
        private byte[] pretreeLen;
        private byte[] maintreeLen;
        private byte[] lengthLen;
        private byte[] alignedLen;

        private ushort[] pretreeTable;
        private ushort[] maintreeTable;
        private ushort[] lengthTable;
        private ushort[] alignedTable;

        // Position base and extra bits tables
        private static readonly uint[] PositionBase;
        private static readonly byte[] ExtraBits;

        static LzxDecoder()
        {
            ExtraBits = new byte[52];
            for (int i = 0, j = 0; i <= 50; i += 2)
            {
                ExtraBits[i] = ExtraBits[i + 1] = (byte)j;
                if (i != 0 && j < 17) j++;
            }

            PositionBase = new uint[52];
            for (int i = 0, j = 0; i <= 50; i++)
            {
                PositionBase[i] = (uint)j;
                j += 1 << ExtraBits[i];
            }
        }

        public LzxDecoder(int windowBits)
        {
            if (windowBits < 15 || windowBits > 21)
                throw new ArgumentException("Window bits must be between 15 and 21");

            windowSize = (uint)(1 << windowBits);
            window = new byte[windowSize];
            for (int i = 0; i < (int)windowSize; i++) window[i] = 0xDC;
            windowPos = 0;

            int posnSlots;
            if (windowBits == 20) posnSlots = 42;
            else if (windowBits == 21) posnSlots = 50;
            else posnSlots = windowBits << 1;

            R0 = R1 = R2 = 1;
            mainElements = (ushort)(NUM_CHARS + (posnSlots << 3));
            headerRead = false;
            blockType = BlockType.Invalid;
            blockRemaining = 0;

            pretreeLen = new byte[PRETREE_MAXSYMBOLS];
            maintreeLen = new byte[MAINTREE_MAXSYMBOLS];
            lengthLen = new byte[LENGTH_MAXSYMBOLS];
            alignedLen = new byte[ALIGNED_MAXSYMBOLS];

            pretreeTable = new ushort[(1 << PRETREE_TABLEBITS) + PRETREE_MAXSYMBOLS * 2];
            maintreeTable = new ushort[(1 << MAINTREE_TABLEBITS) + MAINTREE_MAXSYMBOLS * 2];
            lengthTable = new ushort[(1 << LENGTH_TABLEBITS) + LENGTH_MAXSYMBOLS * 2];
            alignedTable = new ushort[(1 << ALIGNED_TABLEBITS) + ALIGNED_MAXSYMBOLS * 2];

            // Zero length arrays
            Array.Clear(maintreeLen, 0, maintreeLen.Length);
            Array.Clear(lengthLen, 0, lengthLen.Length);
        }

        public byte[] DecompressBlock(byte[] inputData, int inputSize, int outputSize)
        {
            BitBuffer bitBuf = new BitBuffer(inputData, inputSize);
            int togo = outputSize;
            int thisRun;

            // Read E8 translation header (once per stream)
            if (!headerRead)
            {
                uint intel = bitBuf.ReadBits(1);
                if (intel != 0)
                {
                    // Read and discard Intel E8 filesize (32 bits)
                    bitBuf.ReadBits(16);
                    bitBuf.ReadBits(16);
                }
                headerRead = true;
            }

            while (togo > 0)
            {
                if (blockRemaining == 0)
                {
                    blockType = (BlockType)bitBuf.ReadBits(3);
                    uint blockLen24 = bitBuf.ReadBits(16);
                    blockLen24 = (blockLen24 << 8) | bitBuf.ReadBits(8);
                    blockRemaining = (int)blockLen24;

                    switch (blockType)
                    {
                        case BlockType.Aligned:
                            for (int i = 0; i < 8; i++)
                                alignedLen[i] = (byte)bitBuf.ReadBits(3);
                            MakeDecodeTable(ALIGNED_MAXSYMBOLS, ALIGNED_TABLEBITS, alignedLen, alignedTable);
                            ReadLengths(bitBuf, maintreeLen, 0, 256);
                            ReadLengths(bitBuf, maintreeLen, 256, mainElements);
                            MakeDecodeTable(MAINTREE_MAXSYMBOLS, MAINTREE_TABLEBITS, maintreeLen, maintreeTable);
                            ReadLengths(bitBuf, lengthLen, 0, NUM_SECONDARY_LENGTHS);
                            MakeDecodeTable(LENGTH_MAXSYMBOLS, LENGTH_TABLEBITS, lengthLen, lengthTable);
                            break;

                        case BlockType.Verbatim:
                            ReadLengths(bitBuf, maintreeLen, 0, 256);
                            ReadLengths(bitBuf, maintreeLen, 256, mainElements);
                            MakeDecodeTable(MAINTREE_MAXSYMBOLS, MAINTREE_TABLEBITS, maintreeLen, maintreeTable);
                            ReadLengths(bitBuf, lengthLen, 0, NUM_SECONDARY_LENGTHS);
                            MakeDecodeTable(LENGTH_MAXSYMBOLS, LENGTH_TABLEBITS, lengthLen, lengthTable);
                            break;

                        case BlockType.Uncompressed:
                            bitBuf.Align();
                            R0 = bitBuf.ReadUInt32LE();
                            R1 = bitBuf.ReadUInt32LE();
                            R2 = bitBuf.ReadUInt32LE();
                            break;
                    }
                }

                thisRun = Math.Min(blockRemaining, togo);
                togo -= thisRun;
                blockRemaining -= thisRun;

                if (blockType == BlockType.Uncompressed)
                {
                    for (int i = 0; i < thisRun; i++)
                    {
                        window[windowPos] = bitBuf.ReadRawByte();
                        windowPos = (windowPos + 1) & (windowSize - 1);
                    }
                }
                else
                {
                    while (thisRun > 0)
                    {
                        int mainSym = ReadHuffSym(bitBuf, maintreeTable, maintreeLen,
                            MAINTREE_MAXSYMBOLS, MAINTREE_TABLEBITS);

                        if (mainSym < NUM_CHARS)
                        {
                            window[windowPos] = (byte)mainSym;
                            windowPos = (windowPos + 1) & (windowSize - 1);
                            thisRun--;
                        }
                        else
                        {
                            mainSym -= NUM_CHARS;
                            int matchLen = mainSym & NUM_PRIMARY_LENGTHS;
                            if (matchLen == NUM_PRIMARY_LENGTHS)
                            {
                                int lengthSym = ReadHuffSym(bitBuf, lengthTable, lengthLen,
                                    LENGTH_MAXSYMBOLS, LENGTH_TABLEBITS);
                                matchLen += lengthSym;
                            }
                            matchLen += MIN_MATCH;

                            int matchOffsetSlot = mainSym >> 3;
                            uint matchOffset;

                            if (matchOffsetSlot > 2)
                            {
                                if (blockType == BlockType.Aligned && ExtraBits[matchOffsetSlot] >= 3)
                                {
                                    int extraBitsCount = ExtraBits[matchOffsetSlot] - 3;
                                    matchOffset = PositionBase[matchOffsetSlot];
                                    if (extraBitsCount > 0)
                                        matchOffset += bitBuf.ReadBits(extraBitsCount) << 3;
                                    int alignedSym = ReadHuffSym(bitBuf, alignedTable, alignedLen,
                                        ALIGNED_MAXSYMBOLS, ALIGNED_TABLEBITS);
                                    matchOffset += (uint)alignedSym;
                                }
                                else if (ExtraBits[matchOffsetSlot] > 0)
                                {
                                    matchOffset = PositionBase[matchOffsetSlot] +
                                                  bitBuf.ReadBits(ExtraBits[matchOffsetSlot]);
                                }
                                else
                                {
                                    matchOffset = PositionBase[matchOffsetSlot];
                                }

                                R2 = R1;
                                R1 = R0;
                                R0 = matchOffset;
                            }
                            else if (matchOffsetSlot == 0)
                            {
                                matchOffset = R0;
                            }
                            else if (matchOffsetSlot == 1)
                            {
                                matchOffset = R1;
                                R1 = R0;
                                R0 = matchOffset;
                            }
                            else // matchOffsetSlot == 2
                            {
                                matchOffset = R2;
                                R2 = R0;
                                R0 = matchOffset;
                            }

                            int runDest = (int)windowPos;
                            int runSrc = (int)((windowPos - matchOffset) & (windowSize - 1));
                            thisRun -= matchLen;

                            // Copy match (byte by byte for overlapping)
                            for (int i = 0; i < matchLen; i++)
                            {
                                window[(uint)runDest & (windowSize - 1)] =
                                    window[(uint)runSrc & (windowSize - 1)];
                                runDest++;
                                runSrc++;
                            }
                            windowPos = (uint)runDest & (windowSize - 1);
                        }
                    }
                }
            }

            // Copy from window to output
            byte[] output = new byte[outputSize];
            if (windowPos == 0)
            {
                Array.Copy(window, (int)(windowSize - outputSize), output, 0, outputSize);
            }
            else if (windowPos >= (uint)outputSize)
            {
                Array.Copy(window, (int)(windowPos - outputSize), output, 0, outputSize);
            }
            else
            {
                int tailLen = outputSize - (int)windowPos;
                Array.Copy(window, (int)(windowSize - tailLen), output, 0, tailLen);
                Array.Copy(window, 0, output, tailLen, (int)windowPos);
            }

            return output;
        }

        private void ReadLengths(BitBuffer bitBuf, byte[] lens, int first, int last)
        {
            // Read pretree
            for (int i = 0; i < PRETREE_NUM_ELEMENTS; i++)
                pretreeLen[i] = (byte)bitBuf.ReadBits(4);
            MakeDecodeTable(PRETREE_MAXSYMBOLS, PRETREE_TABLEBITS, pretreeLen, pretreeTable);

            for (int x = first; x < last;)
            {
                int z = ReadHuffSym(bitBuf, pretreeTable, pretreeLen, PRETREE_MAXSYMBOLS, PRETREE_TABLEBITS);

                if (z == 17)
                {
                    int n = (int)bitBuf.ReadBits(4) + 4;
                    while (n-- > 0 && x < last)
                        lens[x++] = 0;
                }
                else if (z == 18)
                {
                    int n = (int)bitBuf.ReadBits(5) + 20;
                    while (n-- > 0 && x < last)
                        lens[x++] = 0;
                }
                else if (z == 19)
                {
                    int n = (int)bitBuf.ReadBits(1) + 4;
                    z = ReadHuffSym(bitBuf, pretreeTable, pretreeLen, PRETREE_MAXSYMBOLS, PRETREE_TABLEBITS);
                    z = (lens[x] - z + 17) % 17;
                    while (n-- > 0 && x < last)
                        lens[x++] = (byte)z;
                }
                else
                {
                    z = (lens[x] - z + 17) % 17;
                    lens[x++] = (byte)z;
                }
            }
        }

        private int ReadHuffSym(BitBuffer bitBuf, ushort[] table, byte[] lengths,
            int maxSymbols, int tableBits)
        {
            bitBuf.EnsureBits(16);
            uint val = bitBuf.PeekBits(tableBits);
            int sym = table[val];

            if (sym >= maxSymbols)
            {
                uint j = (uint)(1 << (32 - tableBits - 1));
                do
                {
                    sym = table[sym * 2 + ((bitBuf.Buffer & j) != 0 ? 1 : 0)];
                    j >>= 1;
                } while (sym >= maxSymbols);
            }

            bitBuf.RemoveBits(lengths[sym]);
            return sym;
        }

        private static void MakeDecodeTable(int nsyms, int nbits, byte[] length, ushort[] table)
        {
            int tableSize = 1 << nbits;

            // Clear table
            for (int i = 0; i < table.Length; i++) table[i] = 0;

            // Fill in table for codes that fit in nbits
            int bitNum = 1;
            uint pos = 0;
            while (bitNum <= nbits)
            {
                for (int sym = 0; sym < nsyms; sym++)
                {
                    if (length[sym] == bitNum)
                    {
                        int leaf = (int)pos;
                        pos += (uint)(1 << (nbits - bitNum));
                        if (pos > (uint)tableSize) return;
                        int fill = 1 << (nbits - bitNum);
                        for (int i = 0; i < fill; i++)
                            table[leaf++] = (ushort)sym;
                    }
                }
                bitNum++;
            }

            if (pos == (uint)tableSize)
                return;

            // Mark remaining entries as unused
            for (int sym = (int)pos; sym < tableSize; sym++)
                table[sym] = 0xFFFF;

            // Fill overflow entries for codes longer than nbits
            uint nextSym = (uint)tableSize;
            pos <<= 16;
            bitNum = nbits + 1;

            while (bitNum <= 16)
            {
                for (int sym = 0; sym < nsyms; sym++)
                {
                    if (length[sym] == bitNum)
                    {
                        uint leaf = pos >> 16;

                        for (int fill = 0; fill < (bitNum - nbits); fill++)
                        {
                            if (table[leaf] == 0xFFFF)
                            {
                                table[nextSym * 2] = 0xFFFF;
                                table[nextSym * 2 + 1] = 0xFFFF;
                                table[leaf] = (ushort)nextSym;
                                nextSym++;
                            }
                            leaf = (uint)(table[leaf] * 2 + ((pos >> (15 - fill)) & 1));
                        }
                        table[leaf] = (ushort)sym;
                        pos += (uint)(1 << (16 - bitNum));
                    }
                }
                bitNum++;
            }
        }
    }

    /// <summary>
    /// Bit-level reader for LZX decompression.
    /// LZX uses MSB-first bit reading with 16-bit LE word fetches.
    /// The buffer stores bits MSB-first: bit position 31 is the "next" bit to read.
    /// </summary>
    internal class BitBuffer
    {
        private byte[] data;
        private int dataPos;
        private int dataLen;
        private uint buffer;
        private int bitsLeft;

        /// <summary>Current bit buffer value (public for tree walking)</summary>
        public uint Buffer { get { return buffer; } }

        public BitBuffer(byte[] data, int length)
        {
            this.data = data;
            this.dataLen = Math.Min(length, data.Length);
            this.dataPos = 0;
            this.buffer = 0;
            this.bitsLeft = 0;
            EnsureBits(16);
        }

        /// <summary>
        /// Ensure at least 'count' bits are in the buffer.
        /// LZX reads 16-bit words in little-endian order, but the bits
        /// within each word are ordered MSB-first.
        /// </summary>
        public void EnsureBits(int count)
        {
            while (bitsLeft < count)
            {
                if (dataPos + 1 < dataLen)
                {
                    // Read 16-bit word, little-endian
                    uint word = (uint)(data[dataPos] | (data[dataPos + 1] << 8));
                    dataPos += 2;
                    // Insert into buffer at the LSB side (bits shift left as we consume)
                    buffer |= word << (16 - bitsLeft);
                    bitsLeft += 16;
                }
                else if (dataPos < dataLen)
                {
                    // Only one byte left
                    buffer |= (uint)data[dataPos] << (24 - bitsLeft);
                    dataPos++;
                    bitsLeft += 8;
                }
                else
                {
                    return; // No more data
                }
            }
        }

        public uint ReadBits(int count)
        {
            EnsureBits(count);
            uint val = buffer >> (32 - count);
            buffer <<= count;
            bitsLeft -= count;
            return val;
        }

        public uint PeekBits(int count)
        {
            EnsureBits(count);
            return buffer >> (32 - count);
        }

        public void RemoveBits(int count)
        {
            buffer <<= count;
            bitsLeft -= count;
        }

        /// <summary>
        /// Align to next 16-bit boundary (discard remaining bits in current word)
        /// </summary>
        public void Align()
        {
            int discard = bitsLeft & 15;
            if (discard > 0)
            {
                buffer <<= discard;
                bitsLeft -= discard;
            }
        }

        /// <summary>
        /// Read a raw 32-bit little-endian value (for uncompressed blocks)
        /// </summary>
        public uint ReadUInt32LE()
        {
            // Flush bit buffer first
            bitsLeft = 0;
            buffer = 0;

            if (dataPos + 3 < dataLen)
            {
                uint val = (uint)(data[dataPos] | (data[dataPos + 1] << 8) |
                                   (data[dataPos + 2] << 16) | (data[dataPos + 3] << 24));
                dataPos += 4;
                return val;
            }
            return 0;
        }

        /// <summary>
        /// Read a raw byte (for uncompressed blocks)
        /// </summary>
        public byte ReadRawByte()
        {
            if (dataPos < dataLen)
                return data[dataPos++];
            return 0;
        }
    }

    /// <summary>
    /// Simple LZ4 decompressor for XNB files that use LZ4 compression (MonoGame 3.8+)
    /// </summary>
    public static class Lz4Decompressor
    {
        public static byte[] Decompress(byte[] input, int decompressedSize)
        {
            byte[] output = new byte[decompressedSize];
            int srcIdx = 0;
            int dstIdx = 0;

            while (srcIdx < input.Length && dstIdx < decompressedSize)
            {
                byte token = input[srcIdx++];

                // Literal length
                int literalLen = (token >> 4) & 0x0F;
                if (literalLen == 15)
                {
                    int extra;
                    do
                    {
                        if (srcIdx >= input.Length) break;
                        extra = input[srcIdx++];
                        literalLen += extra;
                    } while (extra == 255);
                }

                // Copy literals
                for (int i = 0; i < literalLen && dstIdx < decompressedSize && srcIdx < input.Length; i++)
                    output[dstIdx++] = input[srcIdx++];

                if (dstIdx >= decompressedSize) break;
                if (srcIdx + 1 >= input.Length) break;

                // Match offset (2 bytes LE)
                int offset = input[srcIdx] | (input[srcIdx + 1] << 8);
                srcIdx += 2;

                if (offset == 0) break; // Invalid

                // Match length
                int matchLen = (token & 0x0F) + 4;
                if ((token & 0x0F) == 15)
                {
                    int extra;
                    do
                    {
                        if (srcIdx >= input.Length) break;
                        extra = input[srcIdx++];
                        matchLen += extra;
                    } while (extra == 255);
                }

                // Copy match (byte-by-byte for overlapping matches)
                int matchStart = dstIdx - offset;
                for (int i = 0; i < matchLen && dstIdx < decompressedSize; i++)
                {
                    output[dstIdx] = output[matchStart + (i % offset)];
                    dstIdx++;
                }
            }

            return output;
        }
    }
}
