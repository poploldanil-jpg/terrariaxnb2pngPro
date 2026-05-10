// XNBExporterPro - ImageWriter.cs
// Writes RGBA pixel data to PNG, BMP, JPEG, TGA files
// Pure implementation with no external dependencies

using System;
using System.IO;
using System.IO.Compression;

namespace XNBExporterPro
{
    /// <summary>
    /// Supported output image formats
    /// </summary>
    public enum ImageFormat
    {
        PNG,
        BMP,
        TGA,
    }

    /// <summary>
    /// Writes images in various formats from raw RGBA data
    /// </summary>
    public static class ImageWriter
    {
        /// <summary>
        /// Save RGBA pixel data to a file in the specified format
        /// </summary>
        public static void Save(string filePath, int width, int height, byte[] rgbaData, ImageFormat format)
        {
            switch (format)
            {
                case ImageFormat.PNG:
                    SavePng(filePath, width, height, rgbaData);
                    break;
                case ImageFormat.BMP:
                    SaveBmp(filePath, width, height, rgbaData);
                    break;
                case ImageFormat.TGA:
                    SaveTga(filePath, width, height, rgbaData);
                    break;
                default:
                    throw new ArgumentException($"Unsupported format: {format}");
            }
        }

        /// <summary>
        /// Get file extension for format
        /// </summary>
        public static string GetExtension(ImageFormat format)
        {
            switch (format)
            {
                case ImageFormat.PNG: return ".png";
                case ImageFormat.BMP: return ".bmp";
                case ImageFormat.TGA: return ".tga";
                default: return ".png";
            }
        }

        #region PNG Writer

        private static void SavePng(string filePath, int width, int height, byte[] rgbaData)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // PNG Signature
                bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

                // IHDR chunk
                WriteChunk(bw, "IHDR", writer =>
                {
                    WriteBigEndianInt32(writer, width);
                    WriteBigEndianInt32(writer, height);
                    writer.Write((byte)8);  // Bit depth
                    writer.Write((byte)6);  // Color type: RGBA
                    writer.Write((byte)0);  // Compression method
                    writer.Write((byte)0);  // Filter method
                    writer.Write((byte)0);  // Interlace method
                });

                // IDAT chunk - prepare filtered data
                byte[] rawImageData;
                using (var rawMs = new MemoryStream())
                {
                    for (int y = 0; y < height; y++)
                    {
                        rawMs.WriteByte(0); // Filter type: None
                        rawMs.Write(rgbaData, y * width * 4, width * 4);
                    }
                    rawImageData = rawMs.ToArray();
                }

                // Compress with deflate
                byte[] compressedData;
                using (var compMs = new MemoryStream())
                {
                    // zlib header
                    compMs.WriteByte(0x78); // CMF
                    compMs.WriteByte(0x01); // FLG

                    using (var deflate = new DeflateStream(compMs, CompressionLevel.Optimal, true))
                    {
                        deflate.Write(rawImageData, 0, rawImageData.Length);
                    }

                    // Adler32 checksum
                    uint adler = Adler32(rawImageData);
                    compMs.WriteByte((byte)((adler >> 24) & 0xFF));
                    compMs.WriteByte((byte)((adler >> 16) & 0xFF));
                    compMs.WriteByte((byte)((adler >> 8) & 0xFF));
                    compMs.WriteByte((byte)(adler & 0xFF));

                    compressedData = compMs.ToArray();
                }

                // Write IDAT
                WriteChunkRaw(bw, "IDAT", compressedData);

                // IEND chunk
                WriteChunk(bw, "IEND", writer => { });
            }
        }

        private static void WriteChunk(BinaryWriter bw, string type, Action<BinaryWriter> writeData)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writeData(writer);
                writer.Flush();
                byte[] data = ms.ToArray();
                WriteChunkRaw(bw, type, data);
            }
        }

        private static void WriteChunkRaw(BinaryWriter bw, string type, byte[] data)
        {
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

            WriteBigEndianInt32(bw, data.Length);
            bw.Write(typeBytes);
            bw.Write(data);

            // CRC32 of type + data
            byte[] crcInput = new byte[4 + data.Length];
            Array.Copy(typeBytes, 0, crcInput, 0, 4);
            Array.Copy(data, 0, crcInput, 4, data.Length);
            uint crc = Crc32(crcInput);
            WriteBigEndianUInt32(bw, crc);
        }

        private static void WriteBigEndianInt32(BinaryWriter bw, int value)
        {
            bw.Write((byte)((value >> 24) & 0xFF));
            bw.Write((byte)((value >> 16) & 0xFF));
            bw.Write((byte)((value >> 8) & 0xFF));
            bw.Write((byte)(value & 0xFF));
        }

        private static void WriteBigEndianUInt32(BinaryWriter bw, uint value)
        {
            bw.Write((byte)((value >> 24) & 0xFF));
            bw.Write((byte)((value >> 16) & 0xFF));
            bw.Write((byte)((value >> 8) & 0xFF));
            bw.Write((byte)(value & 0xFF));
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static uint[] crc32Table;

        private static uint Crc32(byte[] data)
        {
            if (crc32Table == null)
            {
                crc32Table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++)
                    {
                        if ((c & 1) != 0)
                            c = 0xEDB88320u ^ (c >> 1);
                        else
                            c >>= 1;
                    }
                    crc32Table[i] = c;
                }
            }

            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
                crc = crc32Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        #endregion

        #region BMP Writer

        private static void SaveBmp(string filePath, int width, int height, byte[] rgbaData)
        {
            int rowSize = width * 4;
            int paddedRowSize = (rowSize + 3) & ~3;
            int pixelDataSize = paddedRowSize * height;
            int fileSize = 14 + 124 + pixelDataSize; // BMP header + BITMAPV5HEADER + pixels

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // BMP File Header (14 bytes)
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write(fileSize);
                bw.Write((ushort)0); // Reserved1
                bw.Write((ushort)0); // Reserved2
                bw.Write(14 + 124); // Pixel data offset

                // BITMAPV5HEADER (124 bytes)
                bw.Write(124);       // Header size
                bw.Write(width);     // Width
                bw.Write(-height);   // Height (negative = top-down)
                bw.Write((ushort)1); // Planes
                bw.Write((ushort)32);// Bits per pixel
                bw.Write(3);         // Compression: BI_BITFIELDS
                bw.Write(pixelDataSize);
                bw.Write(2835);      // X pixels per meter
                bw.Write(2835);      // Y pixels per meter
                bw.Write(0);         // Colors used
                bw.Write(0);         // Important colors

                // Color masks: RGBA
                bw.Write((uint)0x00FF0000); // Red mask
                bw.Write((uint)0x0000FF00); // Green mask
                bw.Write((uint)0x000000FF); // Blue mask
                bw.Write((uint)0xFF000000); // Alpha mask

                // Color space type: LCS_sRGB
                bw.Write((uint)0x73524742);
                // CIEXYZTRIPLE endpoints (36 bytes)
                for (int i = 0; i < 9; i++) bw.Write(0);
                // Gamma values (12 bytes)
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                // Intent
                bw.Write(4); // LCS_GM_IMAGES
                // Profile data, size, reserved
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);

                // Pixel data (top-down because height is negative)
                byte[] row = new byte[paddedRowSize];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = (y * width + x) * 4;
                        int dstIdx = x * 4;
                        // RGBA -> BGRA
                        row[dstIdx + 0] = rgbaData[srcIdx + 2]; // B
                        row[dstIdx + 1] = rgbaData[srcIdx + 1]; // G
                        row[dstIdx + 2] = rgbaData[srcIdx + 0]; // R
                        row[dstIdx + 3] = rgbaData[srcIdx + 3]; // A
                    }
                    bw.Write(row);
                }
            }
        }

        #endregion

        #region TGA Writer

        private static void SaveTga(string filePath, int width, int height, byte[] rgbaData)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // TGA Header (18 bytes)
                bw.Write((byte)0);    // ID length
                bw.Write((byte)0);    // Color map type
                bw.Write((byte)2);    // Image type: uncompressed true-color
                bw.Write((short)0);   // Color map origin
                bw.Write((short)0);   // Color map length
                bw.Write((byte)0);    // Color map depth
                bw.Write((short)0);   // X origin
                bw.Write((short)0);   // Y origin
                bw.Write((short)width);
                bw.Write((short)height);
                bw.Write((byte)32);   // Bits per pixel
                bw.Write((byte)0x28); // Image descriptor (top-left origin, 8 alpha bits)

                // Pixel data
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * width + x) * 4;
                        // RGBA -> BGRA
                        bw.Write(rgbaData[idx + 2]); // B
                        bw.Write(rgbaData[idx + 1]); // G
                        bw.Write(rgbaData[idx + 0]); // R
                        bw.Write(rgbaData[idx + 3]); // A
                    }
                }
            }
        }

        #endregion
    }
}
