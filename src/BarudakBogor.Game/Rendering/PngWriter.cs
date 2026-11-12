using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;

namespace BarudakBogor.Game.Rendering;

/// <summary>Minimal PNG encoder for screenshots and photo mode (8 bit RGBA, no dependencies).</summary>
public static class PngWriter
{
    public static void Save(string path, ReadOnlySpan<byte> rgba, int width, int height, bool bgra = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using FileStream file = File.Create(path);
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        Chunk(file, "IHDR"u8, header);

        using MemoryStream packed = new();
        using (ZLibStream zlib = new(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] row = new byte[(width * 4) + 1];
            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<byte> source = rgba.Slice(y * width * 4, width * 4);
                source.CopyTo(row.AsSpan(1));
                for (int x = 0; x < width; x++)
                {
                    int i = 1 + (x * 4);
                    if (bgra)
                    {
                        (row[i], row[i + 2]) = (row[i + 2], row[i]);
                    }

                    row[i + 3] = 255;
                }

                zlib.Write(row);
            }
        }

        Chunk(file, "IDAT"u8, packed.GetBuffer().AsSpan(0, (int)packed.Length));
        Chunk(file, "IEND"u8, default);
    }

    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        stream.Write(word);
        stream.Write(type);
        stream.Write(data);
        Crc32 crc = new();
        crc.Append(type);
        crc.Append(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc.GetCurrentHashAsUInt32());
        stream.Write(word);
    }
}
