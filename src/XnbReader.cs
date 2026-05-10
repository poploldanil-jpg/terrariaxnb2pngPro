// XNBExporterPro - XnbReader.cs
// Standalone XNB file parser (no XNA/MonoGame dependency)
// Supports: Texture2D with Color, DXT1, DXT3, DXT5 surface formats
// Supports: LZX and LZ4 compressed XNB files

using System;
using System.IO;
using System.Text;

namespace XNBExporterPro
{
    /// <summary>
    /// Surface format identifiers matching XNA SurfaceFormat enum
    /// </summary>
    public enum SurfaceFormat
    {
        Color = 0,
        Bgr565 = 1,
        Bgra5551 = 2,
        Bgra4444 = 3,
        Dxt1 = 4,
        Dxt3 = 5,
        Dxt5 = 6,
        NormalizedByte2 = 7,
        NormalizedByte4 = 8,
        Rgba1010102 = 9,
        Rg32 = 10,
        Rgba64 = 11,
        Alpha8 = 12,
        Single = 13,
        Vector2 = 14,
        Vector4 = 15,
        HalfSingle = 16,
        HalfVector2 = 17,
        HalfVector4 = 18,
        HdrBlendable = 19,
        Bgr32 = 20,
        Bgra32 = 21,
    }

    [Flags]
    public enum XnbFlags : byte
    {
        None = 0,
        HiDefProfile = 0x01,
        Lz4Compressed = 0x40,
        LzxCompressed = 0x80,
    }

    /// <summary>
    /// Parsed XNB texture data
    /// </summary>
    public class XnbTexture
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public SurfaceFormat Format { get; set; }
        public int MipCount { get; set; }
        public byte[] PixelData { get; set; } // RGBA after decoding
        public string TypeReaderName { get; set; }
        public byte PlatformId { get; set; }
        public byte FormatVersion { get; set; }
        public XnbFlags Flags { get; set; }
    }

    /// <summary>
    /// Reads and parses XNB files without any XNA/MonoGame dependency
    /// </summary>
    public static class XnbReader
    {
        private const int HEADER_SIZE_UNCOMPRESSED = 10;
        private const int HEADER_SIZE_COMPRESSED = 14;

        /// <summary>
        /// Read a 7-bit encoded integer (same as BinaryReader.Read7BitEncodedInt in .NET)
        /// </summary>
        private static int Read7BitEncodedInt(BinaryReader reader)
        {
            int result = 0;
            int bitsRead = 0;
            int value;
            do
            {
                value = reader.ReadByte();
                result |= (value & 0x7F) << bitsRead;
                bitsRead += 7;
            }
            while ((value & 0x80) != 0);
            return result;
        }

        /// <summary>
        /// Read a length-prefixed string using 7-bit encoded length
        /// </summary>
        private static string Read7BitEncodedString(BinaryReader reader)
        {
            int length = Read7BitEncodedInt(reader);
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Parse an XNB file and extract texture data as RGBA pixels
        /// </summary>
        public static XnbTexture ReadTexture(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            return ReadTexture(fileData, filePath);
        }

        /// <summary>
        /// Parse XNB data from byte array
        /// </summary>
        public static XnbTexture ReadTexture(byte[] fileData, string fileName = "")
        {
            using (var ms = new MemoryStream(fileData))
            using (var reader = new BinaryReader(ms))
            {
                // --- Read Header ---
                // Magic: "XNB" (3 bytes)
                byte[] magic = reader.ReadBytes(3);
                if (magic.Length < 3 || magic[0] != 'X' || magic[1] != 'N' || magic[2] != 'B')
                    throw new InvalidDataException($"Not a valid XNB file: {fileName}");

                // Target platform (1 byte): 'w'=Windows, 'x'=Xbox, 'm'=Phone, 'a'=Android, 'i'=iOS
                byte platformId = reader.ReadByte();

                // Format version (1 byte): usually 5
                byte formatVersion = reader.ReadByte();
                if (formatVersion != 4 && formatVersion != 5)
                    throw new InvalidDataException($"Unsupported XNB version: {formatVersion} in {fileName}");

                // Flags (1 byte)
                XnbFlags flags = (XnbFlags)reader.ReadByte();
                bool isLzxCompressed = (flags & XnbFlags.LzxCompressed) != 0;
                bool isLz4Compressed = (flags & XnbFlags.Lz4Compressed) != 0;
                bool isCompressed = isLzxCompressed || isLz4Compressed;

                // Compressed (or total) file size (4 bytes)
                uint compressedSize = reader.ReadUInt32();

                // Decompressed size (4 bytes) - only present if compressed
                uint decompressedSize = compressedSize;
                if (isCompressed)
                {
                    decompressedSize = reader.ReadUInt32();
                }

                // --- Decompress if needed ---
                BinaryReader contentReader;
                if (isLzxCompressed)
                {
                    int headerSize = HEADER_SIZE_COMPRESSED;
                    int compDataSize = (int)(compressedSize - headerSize);
                    byte[] decompressedData = LzxDecompressor.Decompress(reader, compDataSize, (int)decompressedSize);
                    contentReader = new BinaryReader(new MemoryStream(decompressedData));
                }
                else if (isLz4Compressed)
                {
                    int headerSize = HEADER_SIZE_COMPRESSED;
                    int compDataSize = (int)(compressedSize - headerSize);
                    byte[] compData = reader.ReadBytes(compDataSize);
                    byte[] decompressedData = Lz4Decompressor.Decompress(compData, (int)decompressedSize);
                    contentReader = new BinaryReader(new MemoryStream(decompressedData));
                }
                else
                {
                    contentReader = reader;
                }

                // --- Read Type Readers ---
                int typeReaderCount = Read7BitEncodedInt(contentReader);

                string primaryTypeReaderName = "";
                for (int i = 0; i < typeReaderCount; i++)
                {
                    string typeReaderName = Read7BitEncodedString(contentReader);
                    int typeReaderVersion = contentReader.ReadInt32();

                    if (i == 0)
                    {
                        // Strip assembly info
                        int commaIdx = typeReaderName.IndexOf(',');
                        primaryTypeReaderName = commaIdx >= 0
                            ? typeReaderName.Substring(0, commaIdx)
                            : typeReaderName;
                    }
                }

                // Shared resource count
                int sharedResourceCount = Read7BitEncodedInt(contentReader);

                // Primary asset type index (should be 1 for non-null)
                int primaryAssetTypeIndex = Read7BitEncodedInt(contentReader);
                if (primaryAssetTypeIndex == 0)
                    throw new InvalidDataException($"Primary asset is null in {fileName}");

                // --- Read Texture2D Data ---
                if (!primaryTypeReaderName.Contains("Texture2DReader"))
                    throw new InvalidDataException(
                        $"Not a Texture2D asset. Type reader: {primaryTypeReaderName} in {fileName}");

                SurfaceFormat surfaceFormat = (SurfaceFormat)contentReader.ReadInt32();
                int width = contentReader.ReadInt32();
                int height = contentReader.ReadInt32();
                int mipCount = contentReader.ReadInt32();
                int dataSize = contentReader.ReadInt32();

                if (width <= 0 || height <= 0)
                    throw new InvalidDataException($"Invalid texture dimensions: {width}x{height} in {fileName}");

                byte[] rawData = contentReader.ReadBytes(dataSize);

                // --- Decode to RGBA ---
                byte[] rgbaData;
                switch (surfaceFormat)
                {
                    case SurfaceFormat.Color:
                        rgbaData = rawData; // Already RGBA
                        break;
                    case SurfaceFormat.Dxt1:
                        rgbaData = DxtDecoder.DecompressDxt1(rawData, width, height);
                        break;
                    case SurfaceFormat.Dxt3:
                        rgbaData = DxtDecoder.DecompressDxt3(rawData, width, height);
                        break;
                    case SurfaceFormat.Dxt5:
                        rgbaData = DxtDecoder.DecompressDxt5(rawData, width, height);
                        break;
                    case SurfaceFormat.Bgr565:
                        rgbaData = DecodeRgb565(rawData, width, height);
                        break;
                    case SurfaceFormat.Bgra5551:
                        rgbaData = DecodeBgra5551(rawData, width, height);
                        break;
                    case SurfaceFormat.Bgra4444:
                        rgbaData = DecodeBgra4444(rawData, width, height);
                        break;
                    case SurfaceFormat.Alpha8:
                        rgbaData = DecodeAlpha8(rawData, width, height);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Unsupported surface format: {surfaceFormat} in {fileName}");
                }

                if (contentReader != reader)
                    contentReader.Dispose();

                return new XnbTexture
                {
                    Width = width,
                    Height = height,
                    Format = surfaceFormat,
                    MipCount = mipCount,
                    PixelData = rgbaData,
                    TypeReaderName = primaryTypeReaderName,
                    PlatformId = platformId,
                    FormatVersion = formatVersion,
                    Flags = flags
                };
            }
        }

        /// <summary>
        /// Quick check if an XNB file contains a Texture2D
        /// </summary>
        public static bool IsTexture2D(string filePath)
        {
            try
            {
                using (var fs = File.OpenRead(filePath))
                using (var reader = new BinaryReader(fs))
                {
                    byte[] magic = reader.ReadBytes(3);
                    if (magic.Length < 3 || magic[0] != 'X' || magic[1] != 'N' || magic[2] != 'B')
                        return false;
                    return true; // We can't easily check without decompressing
                }
            }
            catch
            {
                return false;
            }
        }

        #region Additional format decoders

        private static byte[] DecodeRgb565(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            int srcIdx = 0;
            for (int i = 0; i < width * height; i++)
            {
                ushort pixel = (ushort)(data[srcIdx] | (data[srcIdx + 1] << 8));
                srcIdx += 2;
                int r = (pixel >> 11) & 0x1F;
                int g = (pixel >> 5) & 0x3F;
                int b = pixel & 0x1F;
                result[i * 4 + 0] = (byte)((r << 3) | (r >> 2));
                result[i * 4 + 1] = (byte)((g << 2) | (g >> 4));
                result[i * 4 + 2] = (byte)((b << 3) | (b >> 2));
                result[i * 4 + 3] = 255;
            }
            return result;
        }

        private static byte[] DecodeBgra5551(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            int srcIdx = 0;
            for (int i = 0; i < width * height; i++)
            {
                ushort pixel = (ushort)(data[srcIdx] | (data[srcIdx + 1] << 8));
                srcIdx += 2;
                int b = (pixel >> 10) & 0x1F;
                int g = (pixel >> 5) & 0x1F;
                int r = pixel & 0x1F;
                int a = (pixel >> 15) & 1;
                result[i * 4 + 0] = (byte)((r << 3) | (r >> 2));
                result[i * 4 + 1] = (byte)((g << 3) | (g >> 2));
                result[i * 4 + 2] = (byte)((b << 3) | (b >> 2));
                result[i * 4 + 3] = (byte)(a * 255);
            }
            return result;
        }

        private static byte[] DecodeBgra4444(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            int srcIdx = 0;
            for (int i = 0; i < width * height; i++)
            {
                ushort pixel = (ushort)(data[srcIdx] | (data[srcIdx + 1] << 8));
                srcIdx += 2;
                int b = (pixel >> 12) & 0xF;
                int g = (pixel >> 8) & 0xF;
                int r = (pixel >> 4) & 0xF;
                int a = pixel & 0xF;
                result[i * 4 + 0] = (byte)((r << 4) | r);
                result[i * 4 + 1] = (byte)((g << 4) | g);
                result[i * 4 + 2] = (byte)((b << 4) | b);
                result[i * 4 + 3] = (byte)((a << 4) | a);
            }
            return result;
        }

        private static byte[] DecodeAlpha8(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                result[i * 4 + 0] = 255;
                result[i * 4 + 1] = 255;
                result[i * 4 + 2] = 255;
                result[i * 4 + 3] = data[i];
            }
            return result;
        }

        #endregion
    }
}
