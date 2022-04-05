namespace QoiFormat;

using System.Drawing;
using System.Text;

public static class QoiEncoder
{
    public const string QOIF_MAGIC = "qoif";
    private static byte[] QoifMagicBytes = Encoding.ASCII.GetBytes(QOIF_MAGIC);

    public static byte[] Encode(Bitmap img)
    {
        int[] colorIndexArr = new int[64];
        (byte R, byte G, byte B, byte A) prev = (0, 0, 0, 255);
        int run = 0;

        using var stream = new MemoryStream();
        stream.Write(QoifMagicBytes);
        stream.WriteByte((byte)((img.Width >> 24) & 0xFF));
        stream.WriteByte((byte)((img.Width >> 16) & 0xFF));
        stream.WriteByte((byte)((img.Width >> 8) & 0xFF));
        stream.WriteByte((byte)(img.Width & 0xFF));
        stream.WriteByte((byte)((img.Height >> 24) & 0xFF));
        stream.WriteByte((byte)((img.Height >> 16) & 0xFF));
        stream.WriteByte((byte)((img.Height >> 8) & 0xFF));
        stream.WriteByte((byte)(img.Height & 0xFF));
        stream.WriteByte(4);
        stream.WriteByte(0);

        for (int iy = 0; iy < img.Height; iy++)
        {
            for (int ix = 0; ix < img.Width; ix++)
            {
                var pixel = img.GetPixel(ix, iy);
                int arrIndex = (pixel.R * 3 + pixel.G * 5 + pixel.B * 7 + pixel.A * 11) % 64;
                int pixelIndex = GetIndex(ix, iy, img.Width);

                if (pixel.R == prev.R && pixel.G == prev.G && pixel.B == prev.B && pixel.A == prev.A)
                {
                    run++;
                    if (run == 62)
                    {
                        run--;
                        stream.WriteByte((byte)(0b11000000 | run));
                        run = 0;
                    }
                }
                else
                {
                    if (run > 0)
                    {
                        run--;
                        stream.WriteByte((byte)(0b11000000 | run));
                        run = 0;
                    }

                    var compareCoords = GetCoord(colorIndexArr[arrIndex], img.Width);
                    var comparePixel = img.GetPixel(compareCoords.x, compareCoords.y);

                    (int R, int G, int B) diff = (pixel.R - prev.R, pixel.G - prev.G, pixel.B - prev.B);

                    int lumaG = pixel.G - prev.G;
                    int lumaR = (pixel.R - prev.R) - lumaG;
                    int lumaB = (pixel.B - prev.B) - lumaG;

                    if (pixel.R == comparePixel.R && pixel.G == comparePixel.G && pixel.B == comparePixel.B && pixel.A == comparePixel.A && pixelIndex > 0)
                    {
                        stream.WriteByte((byte)arrIndex);
                        // stream.WriteByte(0b11111111);
                        // stream.WriteByte(pixel.R);
                        // stream.WriteByte(pixel.G);
                        // stream.WriteByte(pixel.B);
                        // stream.WriteByte(pixel.A);
                    }
                    else if (-2 <= diff.R && diff.R < 1 && -2 <= diff.G && diff.G < 1 && -2 <= diff.B && diff.B < 1 && pixel.A == prev.A)
                    {
                        stream.WriteByte((byte)(0b01000000 | ((diff.R + 2) << 4) | ((diff.G + 2) << 2) | (diff.B + 2)));
                        // stream.WriteByte(0b11111111);
                        // stream.WriteByte(pixel.R);
                        // stream.WriteByte(pixel.G);
                        // stream.WriteByte(pixel.B);
                        // stream.WriteByte(pixel.A);
                    }
                    else if (-32 <= lumaG && lumaG <= 31 && -8 <= lumaR && lumaR <= 7 && -8 <= lumaB && lumaB <= 7 && pixel.A == prev.A)
                    {
                        stream.WriteByte((byte)(0b10000000 | ((byte)(lumaG + 32))));
                        stream.WriteByte((byte)((((byte)(lumaR + 8)) << 4) | ((byte)(lumaB + 8))));
                        // stream.WriteByte(0b11111111);
                        // stream.WriteByte(pixel.R);
                        // stream.WriteByte(pixel.G);
                        // stream.WriteByte(pixel.B);
                        // stream.WriteByte(pixel.A);
                    }
                    else if (pixel.A == prev.A)
                    {
                        stream.WriteByte(0b11111110);
                        stream.WriteByte(pixel.R);
                        stream.WriteByte(pixel.G);
                        stream.WriteByte(pixel.B);
                    }
                    else
                    {
                        stream.WriteByte(0b11111111);
                        stream.WriteByte(pixel.R);
                        stream.WriteByte(pixel.G);
                        stream.WriteByte(pixel.B);
                        stream.WriteByte(pixel.A);
                    }

                    prev = (pixel.R, pixel.G, pixel.B, pixel.A);
                }

                colorIndexArr[arrIndex] = pixelIndex;
            }
        }

        for (int i = 0; i < 7; i++)
            stream.WriteByte(0);
        stream.WriteByte(1);

        return stream.ToArray();
    }

    public static Bitmap Decode(byte[] bytes)
    {
        for (int i = 0; i < QoifMagicBytes.Length; i++)
            if (QoifMagicBytes[i] != bytes[i])
                throw new FormatException("QOIF magic bytes was incorrect");

        int width = (bytes[4] << 24) | (bytes[5] << 16) | (bytes[6] << 8) | bytes[7];
        int height = (bytes[8] << 24) | (bytes[9] << 16) | (bytes[10] << 8) | bytes[11];
        Console.WriteLine($"{width}:{height}");

        var bitmap = new Bitmap(width, height);

        int[] colorIndexArr = new int[64];
        (byte R, byte G, byte B, byte A) pixel = (0, 0, 0, 255);
        int imageIndex = 0;

        for (int i = 14; i < bytes.Length - 8; i++)
        {
            var coords = GetCoord(imageIndex, width);
            int tag = bytes[i] >> 6;

            if (bytes[i] == 0b11111111)
            {
                pixel = (bytes[++i], bytes[++i], bytes[++i], bytes[++i]);
            }
            else if (bytes[i] == 0b11111110)
            {
                pixel = (bytes[++i], bytes[++i], bytes[++i], pixel.A);
            }
            else if (tag == 0b00)
            {
                int chunkIndex = bytes[i] & 0b00111111;
                int index = colorIndexArr[chunkIndex];
                var refCoords = GetCoord(index, width);
                var color = bitmap.GetPixel(refCoords.x, refCoords.y);
                pixel = (color.R, color.G, color.B, color.A);
            }
            else if (tag == 0b01)
            {
                int dr = (bytes[i] >> 4) & 0b11;
                int dg = (bytes[i] >> 2) & 0b11;
                int db = bytes[i] & 0b11;
                pixel = ((byte)(pixel.R + dr - 2), (byte)(pixel.G + dg - 2), (byte)(pixel.B + db - 2), pixel.A);
            }
            else if(tag == 0b10)
            {
                var lumaG = (bytes[i] & 0b111111) - 32;
                var lumaR = ((bytes[++i] >> 4) & 0b1111) - 8;
                var lumaB = (bytes[i] & 0b1111) - 8;

                var G = lumaG + pixel.G;
                var R = lumaR + pixel.R + lumaG;
                var B = lumaB + pixel.B + lumaG;
                
                pixel = ((byte)R, (byte)G, (byte)B, pixel.A);
            }
            else if(tag == 0b11)
            {
                var run = bytes[i] & 0b111111;
                for(int j = 0; j < run; j++)
                {
                    bitmap.SetPixel(coords.x, coords.y, Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B));
                    imageIndex++;
                    coords = GetCoord(imageIndex, width);
                }
            }
            else throw new FormatException($"Something went terribly wrong");

            int arrIndex = (pixel.R * 3 + pixel.G * 5 + pixel.B * 7 + pixel.A * 11) % 64;
            colorIndexArr[arrIndex] = imageIndex;
            
            bitmap.SetPixel(coords.x, coords.y, Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B));
            imageIndex++;
        }

        return bitmap;
    }

    private static int GetIndex(int x, int y, int width)
        => y * width + x;

    private static (int x, int y) GetCoord(int index, int width)
        => (index % width, index / width);
}
