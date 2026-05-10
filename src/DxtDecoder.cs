// XNBExporterPro - DxtDecoder.cs
// DXT1/DXT3/DXT5 texture decompression

using System;

namespace XNBExporterPro
{
    /// <summary>
    /// Decompresses DXT1, DXT3, and DXT5 compressed textures to RGBA
    /// </summary>
    public static class DxtDecoder
    {
        /// <summary>
        /// Decompress DXT1 (BC1) compressed texture data
        /// </summary>
        public static byte[] DecompressDxt1(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int offset = 0;

            for (int y = 0; y < blockCountY; y++)
            {
                for (int x = 0; x < blockCountX; x++)
                {
                    DecompressDxt1Block(data, offset, result, x * 4, y * 4, width, height);
                    offset += 8;
                }
            }
            return result;
        }

        /// <summary>
        /// Decompress DXT3 (BC2) compressed texture data
        /// </summary>
        public static byte[] DecompressDxt3(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int offset = 0;

            for (int y = 0; y < blockCountY; y++)
            {
                for (int x = 0; x < blockCountX; x++)
                {
                    DecompressDxt3Block(data, offset, result, x * 4, y * 4, width, height);
                    offset += 16;
                }
            }
            return result;
        }

        /// <summary>
        /// Decompress DXT5 (BC3) compressed texture data
        /// </summary>
        public static byte[] DecompressDxt5(byte[] data, int width, int height)
        {
            byte[] result = new byte[width * height * 4];
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int offset = 0;

            for (int y = 0; y < blockCountY; y++)
            {
                for (int x = 0; x < blockCountX; x++)
                {
                    DecompressDxt5Block(data, offset, result, x * 4, y * 4, width, height);
                    offset += 16;
                }
            }
            return result;
        }

        #region DXT1 Block

        private static void DecompressDxt1Block(byte[] data, int offset, byte[] result,
            int blockX, int blockY, int width, int height)
        {
            ushort c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
            ushort c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));
            uint lookupTable = (uint)(data[offset + 4] | (data[offset + 5] << 8) |
                                       (data[offset + 6] << 16) | (data[offset + 7] << 24));

            byte r0, g0, b0, r1, g1, b1;
            UnpackRgb565(c0, out r0, out g0, out b0);
            UnpackRgb565(c1, out r1, out g1, out b1);

            byte[][] colors = new byte[4][];
            colors[0] = new byte[] { r0, g0, b0, 255 };
            colors[1] = new byte[] { r1, g1, b1, 255 };

            if (c0 > c1)
            {
                colors[2] = new byte[]
                {
                    (byte)((2 * r0 + r1) / 3),
                    (byte)((2 * g0 + g1) / 3),
                    (byte)((2 * b0 + b1) / 3),
                    255
                };
                colors[3] = new byte[]
                {
                    (byte)((r0 + 2 * r1) / 3),
                    (byte)((g0 + 2 * g1) / 3),
                    (byte)((b0 + 2 * b1) / 3),
                    255
                };
            }
            else
            {
                colors[2] = new byte[]
                {
                    (byte)((r0 + r1) / 2),
                    (byte)((g0 + g1) / 2),
                    (byte)((b0 + b1) / 2),
                    255
                };
                colors[3] = new byte[] { 0, 0, 0, 0 }; // Transparent
            }

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int px = blockX + col;
                    int py = blockY + row;
                    if (px >= width || py >= height) continue;

                    int idx = (int)((lookupTable >> (2 * (4 * row + col))) & 0x03);
                    int destIdx = (py * width + px) * 4;
                    result[destIdx + 0] = colors[idx][0]; // R
                    result[destIdx + 1] = colors[idx][1]; // G
                    result[destIdx + 2] = colors[idx][2]; // B
                    result[destIdx + 3] = colors[idx][3]; // A
                }
            }
        }

        #endregion

        #region DXT3 Block

        private static void DecompressDxt3Block(byte[] data, int offset, byte[] result,
            int blockX, int blockY, int width, int height)
        {
            // First 8 bytes: explicit alpha (4 bits per pixel)
            ulong alphaData = 0;
            for (int i = 0; i < 8; i++)
                alphaData |= ((ulong)data[offset + i]) << (8 * i);

            // Next 8 bytes: DXT1 color block
            ushort c0 = (ushort)(data[offset + 8] | (data[offset + 9] << 8));
            ushort c1 = (ushort)(data[offset + 10] | (data[offset + 11] << 8));
            uint lookupTable = (uint)(data[offset + 12] | (data[offset + 13] << 8) |
                                       (data[offset + 14] << 16) | (data[offset + 15] << 24));

            byte r0, g0, b0, r1, g1, b1;
            UnpackRgb565(c0, out r0, out g0, out b0);
            UnpackRgb565(c1, out r1, out g1, out b1);

            byte[][] colors = new byte[4][];
            colors[0] = new byte[] { r0, g0, b0 };
            colors[1] = new byte[] { r1, g1, b1 };
            colors[2] = new byte[]
            {
                (byte)((2 * r0 + r1) / 3),
                (byte)((2 * g0 + g1) / 3),
                (byte)((2 * b0 + b1) / 3)
            };
            colors[3] = new byte[]
            {
                (byte)((r0 + 2 * r1) / 3),
                (byte)((g0 + 2 * g1) / 3),
                (byte)((b0 + 2 * b1) / 3)
            };

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int px = blockX + col;
                    int py = blockY + row;
                    if (px >= width || py >= height) continue;

                    int idx = (int)((lookupTable >> (2 * (4 * row + col))) & 0x03);
                    int alphaIdx = 4 * (4 * row + col);
                    int alpha = (int)((alphaData >> alphaIdx) & 0x0F);
                    alpha = (alpha << 4) | alpha; // Expand to 8 bits

                    int destIdx = (py * width + px) * 4;
                    result[destIdx + 0] = colors[idx][0];
                    result[destIdx + 1] = colors[idx][1];
                    result[destIdx + 2] = colors[idx][2];
                    result[destIdx + 3] = (byte)alpha;
                }
            }
        }

        #endregion

        #region DXT5 Block

        private static void DecompressDxt5Block(byte[] data, int offset, byte[] result,
            int blockX, int blockY, int width, int height)
        {
            // First 2 bytes: alpha endpoints
            byte alpha0 = data[offset];
            byte alpha1 = data[offset + 1];

            // Next 6 bytes: alpha indices (3 bits each, 16 pixels)
            ulong alphaBits = 0;
            for (int i = 0; i < 6; i++)
                alphaBits |= ((ulong)data[offset + 2 + i]) << (8 * i);

            // Alpha lookup table
            byte[] alphaTable = new byte[8];
            alphaTable[0] = alpha0;
            alphaTable[1] = alpha1;
            if (alpha0 > alpha1)
            {
                alphaTable[2] = (byte)((6 * alpha0 + 1 * alpha1) / 7);
                alphaTable[3] = (byte)((5 * alpha0 + 2 * alpha1) / 7);
                alphaTable[4] = (byte)((4 * alpha0 + 3 * alpha1) / 7);
                alphaTable[5] = (byte)((3 * alpha0 + 4 * alpha1) / 7);
                alphaTable[6] = (byte)((2 * alpha0 + 5 * alpha1) / 7);
                alphaTable[7] = (byte)((1 * alpha0 + 6 * alpha1) / 7);
            }
            else
            {
                alphaTable[2] = (byte)((4 * alpha0 + 1 * alpha1) / 5);
                alphaTable[3] = (byte)((3 * alpha0 + 2 * alpha1) / 5);
                alphaTable[4] = (byte)((2 * alpha0 + 3 * alpha1) / 5);
                alphaTable[5] = (byte)((1 * alpha0 + 4 * alpha1) / 5);
                alphaTable[6] = 0;
                alphaTable[7] = 255;
            }

            // Color block (same as DXT1 without 1-bit alpha)
            ushort c0 = (ushort)(data[offset + 8] | (data[offset + 9] << 8));
            ushort c1 = (ushort)(data[offset + 10] | (data[offset + 11] << 8));
            uint lookupTable = (uint)(data[offset + 12] | (data[offset + 13] << 8) |
                                       (data[offset + 14] << 16) | (data[offset + 15] << 24));

            byte r0, g0, b0, r1, g1, b1;
            UnpackRgb565(c0, out r0, out g0, out b0);
            UnpackRgb565(c1, out r1, out g1, out b1);

            byte[][] colors = new byte[4][];
            colors[0] = new byte[] { r0, g0, b0 };
            colors[1] = new byte[] { r1, g1, b1 };
            colors[2] = new byte[]
            {
                (byte)((2 * r0 + r1) / 3),
                (byte)((2 * g0 + g1) / 3),
                (byte)((2 * b0 + b1) / 3)
            };
            colors[3] = new byte[]
            {
                (byte)((r0 + 2 * r1) / 3),
                (byte)((g0 + 2 * g1) / 3),
                (byte)((b0 + 2 * b1) / 3)
            };

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int px = blockX + col;
                    int py = blockY + row;
                    if (px >= width || py >= height) continue;

                    int colorIdx = (int)((lookupTable >> (2 * (4 * row + col))) & 0x03);
                    int alphaOffset = 3 * (4 * row + col);
                    int alphaIdx = (int)((alphaBits >> alphaOffset) & 0x07);

                    int destIdx = (py * width + px) * 4;
                    result[destIdx + 0] = colors[colorIdx][0];
                    result[destIdx + 1] = colors[colorIdx][1];
                    result[destIdx + 2] = colors[colorIdx][2];
                    result[destIdx + 3] = alphaTable[alphaIdx];
                }
            }
        }

        #endregion

        #region Helpers

        private static void UnpackRgb565(ushort color, out byte r, out byte g, out byte b)
        {
            int r5 = (color >> 11) & 0x1F;
            int g6 = (color >> 5) & 0x3F;
            int b5 = color & 0x1F;
            r = (byte)((r5 << 3) | (r5 >> 2));
            g = (byte)((g6 << 2) | (g6 >> 4));
            b = (byte)((b5 << 3) | (b5 >> 2));
        }

        #endregion
    }
}
