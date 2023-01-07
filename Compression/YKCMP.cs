using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Falcom.Compression
{
    public class YKCMP
    {
        private static string Magic = "YKCMP_V1";
        private static int Unknown { get; set; }
        private static int CompressedSize { get; set; }
        private static int UncompressedSize { get; set; }
        public static byte[] Decompress(byte[] input)
        {
            using(BinaryReader reader = new BinaryReader(new MemoryStream(input)))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(8)) != Magic)
                {
                    Console.WriteLine("Unsupported format.");
                    return input;
                }
                Unknown = reader.ReadInt32();
                CompressedSize = reader.ReadInt32();
                UncompressedSize = reader.ReadInt32();
                byte[] output = new byte[UncompressedSize];
                using(BinaryWriter writer = new BinaryWriter(new MemoryStream(output)))
                {
                    while(reader.BaseStream.Position < CompressedSize && writer.BaseStream.Position < UncompressedSize)
                    {
                        byte flag = reader.ReadByte();

                        if (flag < 0x80)
                        {
                            byte[] data = reader.ReadBytes(flag);
                            writer.Write(data);
                        }
                        else
                        {
                            int size;
                            int offset;
                            if (flag < 0xC0)
                            {
                                size = (flag >> 4) - 0x08 + 0x01;
                                offset = (flag & 0x0F) + 0x01;
                            }
                            else if (flag < 0xE0)
                            {
                                byte tmp = reader.ReadByte();
                                size = flag - 0xC0 + 0x02;
                                offset = tmp + 0x01;
                            }
                            else
                            {
                                byte tmp = reader.ReadByte();
                                byte tmp2 = reader.ReadByte();
                                size = (flag << 4) + (tmp >> 4) - 0xE00 + 0x03;
                                offset = ((tmp & 0x0F) << 8) + tmp2 + 0x01;
                            }
                            var savepos = writer.BaseStream.Position;
                            writer.BaseStream.Seek(-offset, SeekOrigin.Current);
                            byte[] data = new byte[size];
                            writer.BaseStream.Read(data, 0, size);
                            writer.BaseStream.Position = savepos;
                            writer.Write(data);
                        }
                    }
                }
                return output;
            }
        }

        public static byte[] Compress(byte[] input)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            writer.Write(4);
            //compressedSize skip
            writer.BaseStream.Position += 4;
            writer.Write(input.Length);

            //compression
            int pos = 0;
            List<byte> literals = new List<byte>();
            int compressedSize = 0x14;
            while(pos < input.Length)
            {
                MatchInfo match = FindMatch(input, pos);
                if (match.Size == 0)
                {
                    if (literals.Count == 0x7F)
                    {
                        int writeCount = WriteLiterals(writer, literals);
                        compressedSize += writeCount;
                        literals.Clear();
                    }
                    literals.Add(input[pos]);
                    pos++;
                    continue;
                }
                if (literals.Count > 0)
                {
                    int writeCount = WriteLiterals(writer, literals);
                    compressedSize += writeCount;
                    literals.Clear();
                }

                if (match.Size <= 0x04 && match.Offset <= 0x10)
                {
                    int byte1 = (match.Size << 4) + 0x70 + (match.Offset - 1);
                    writer.Write((byte)byte1);
                    compressedSize += 1;
                }
                else if (match.Size <= 0x21 && match.Offset <= 0x100)
                {
                    int byte1 = match.Size + 0xC0 - 2;
                    int byte2 = match.Offset - 1;
                    writer.Write((byte)byte1);
                    writer.Write((byte)byte2);
                    compressedSize += 2;
                }
                else
                {
                    int tmp = match.Size + 0x0E00 - 3;
                    int byte1 = tmp >> 4;
                    int byte2 = ((tmp & 0x0F) << 4) + ((match.Offset - 1) >> 8);
                    int byte3 = match.Offset - 1;
                    writer.Write((byte)byte1);
                    writer.Write((byte)byte2);
                    writer.Write((byte)byte3);
                    compressedSize += 3;
                }
                pos += match.Size;
            }

            if (literals.Count > 0)
            {
                int writeCount = WriteLiterals(writer, literals);
                compressedSize += writeCount;
                literals.Clear();
            }

            writer.BaseStream.Position = 0x0C;
            writer.Write(compressedSize);
            return ms.ToArray();
        }

        private class MatchInfo
        {
            public int Size;
            public int Offset;
        }
        private static int WriteLiterals(BinaryWriter writer, List<byte> literals)
        {
            writer.Write((byte)literals.Count);
            writer.Write(literals.ToArray());
            return literals.Count + 1;
        }
        private static MatchInfo FindMatch(byte[] data, int pos)
        {
            int[] maxSize = new[] { 0x03, 0x1F, 0x1FF };
            int[] maxOffset = new[] { 0x10, 0x100, 0x202 };

            MatchInfo result = new MatchInfo { Offset = -1, Size = 0 };

            int size = 0;
            int offset = 0;
            int saved = 0;

            for (int i = 3; i > 0; i--)
            {
                int start = Math.Max(pos - maxOffset[i - 1], 0);
                int end = pos - saved - i;
                int current = start;
                while (current < end)
                {
                    if (data[current] != data[pos])
                    {
                        current++;
                        continue;
                    }

                    int matchSize = MatchLength(data, current, pos, maxSize[i - 1] + i);

                    if ((matchSize - i) > saved)
                    {
                        size = matchSize;
                        offset = pos - current;
                        saved = matchSize - i;
                        end = pos - saved - i;
                    }

                    current++;
                }
            }

            if (saved > 0)
            {
                result.Offset = offset;
                result.Size = size;
            }

            return result;
        }

        private static int MatchLength(byte[] data, int start, int end, int maxSize)
        {
            int currentLength = 0;
            int pos1 = start;
            int pos2 = end;
            while (pos1 < end && pos2 < data.Length && data[pos1] == data[pos2] && currentLength < maxSize)
            {
                pos1++;
                pos2++;
                currentLength++;
            }

            return currentLength;
        }
    }
}
