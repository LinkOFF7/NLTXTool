using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Falcom.Compression;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

namespace nltx_tool
{
    //The Liar Princess and the Blind Prince (PSVita/Switch)
    internal class NLTX
    {
        //Header (0x80 bytes)
        public String Signature = "NMPLTEX1";
        public Int64 Align { get; set; }
        public Int32 Unknown0x10 { get; set; } //always 0x64
        public Int16 PFormat { get; set; } //1 - 8bppIndexed, 2 - RGBA8
        public Int16 Unknown0x16 { get; set; } //vita - 128, switch - 512
        public Int32 Width { get; set; }
        public Int32 Height { get; set; }
        public Byte Unknown0x20 { get; set; } //count?
        public Byte Unknown0x21 { get; set; } //vita - 32, switch - 0
        public Int16 Unknown0x22 { get; set; } //vita - 256, switch - 0
        public Int32 Unknown0x24 { get; set; } //sizeof(Int16)
        public Int32 Unknown0x28 { get; set; } //zero
        public Int32 UncompressedSize { get; set; }

        public void Build(string imageFile)
        {
            //RGBA8 only
            Bitmap source = new Bitmap(imageFile);
            Bitmap bmp = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
            using(BinaryWriter writer = new BinaryWriter(File.Create(imageFile + ".nltx")))
            {
                Console.WriteLine("Compressing image...");
                byte[] pixelData = BitmapToByteArray(bmp);
                byte[] compressed = YKCMP.Compress(pixelData);
                writer.Write(Encoding.ASCII.GetBytes(Signature));
                writer.Write(new Int64());
                writer.Write(0x64);
                writer.Write((Int16)2); //pixel format
                writer.Write((Int16)512);
                writer.Write(bmp.Width);
                writer.Write(bmp.Height);
                writer.Write(1);
                writer.Write(65536);
                writer.Write(0);
                writer.Write(pixelData.Length);
                writer.Write(new byte[0x50]);
                writer.Write(compressed);
            }
        }
        public void Extract(string file)
        {
            using(BinaryReader reader = new BinaryReader(File.OpenRead(file)))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != Signature)
                    throw new Exception("Unknown file format.");
                Align = reader.ReadInt64();
                Unknown0x10 = reader.ReadInt32();
                PFormat = reader.ReadInt16();
                Unknown0x16 = reader.ReadInt16();
                Width = reader.ReadInt32();
                Height = reader.ReadInt32();
                Unknown0x20 = reader.ReadByte();
                Unknown0x21 = reader.ReadByte();
                Unknown0x22 = reader.ReadInt16();
                Unknown0x24 = reader.ReadInt32();
                Unknown0x28 = reader.ReadInt32();
                UncompressedSize = reader.ReadInt32();
                Console.WriteLine("Width: {0}\nHeight: {1}\nPixel Format: {2}", Width, Height, PFormat == 1 ? "8BPP Indexed" : "RGBA8");
                reader.BaseStream.Position += 0x50;
                byte[] decompressed = YKCMP.Decompress(reader.ReadBytes((int)(reader.BaseStream.Length - 0x80)));
                switch (PFormat)
                {
                    case 1:
                        {
                            byte[] pal = new byte[decompressed.Length - UncompressedSize]; //must be 1024 bytes (256-color palette)
                            byte[] pixels = new byte[UncompressedSize];
                            Buffer.BlockCopy(decompressed, 0, pal, 0, pal.Length);
                            Buffer.BlockCopy(decompressed, pal.Length, pixels, 0, pixels.Length);
                            Bitmap bmp = BitmapFromByteArray(pixels, Width, Height, PixelFormat.Format8bppIndexed, pal);
                            bmp.Save(file + ".png", ImageFormat.Png);
                            break;
                        }
                    case 2:
                        {

                            Bitmap bmp = BitmapFromByteArray(decompressed, Width, Height, PixelFormat.Format32bppArgb, null);
                            bmp.Save(file + ".png", ImageFormat.Png);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Unknown pixel data.");
                            return;
                        }
                }
            }
        }

        public Bitmap BitmapFromByteArray(byte[] pixelData, int width, int height, PixelFormat pf, byte[] palette)
        {
            Bitmap bitmap = new Bitmap(width, height, pf);
            ColorPalette pal = bitmap.Palette;
            BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            if(palette != null)
            {
                for (int i = 0, l = 0; i < palette.Length; i += 4, l++)
                {
                    pal.Entries[l] = Color.FromArgb(
                        palette[i + 3],
                        palette[i + 0],
                        palette[i + 1],
                        palette[i + 2]
                        );
                }
                bitmap.Palette = pal;
            }
            IntPtr pNative = bmData.Scan0;
            Marshal.Copy(pixelData, 0, pNative, pixelData.Length);
            bitmap.UnlockBits(bmData);
            return bitmap;
        }

        private byte[] BitmapToByteArray(Bitmap bitmap)
        {
            BitmapData bmpdata = null;
            try
            {
                bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;
                Marshal.Copy(ptr, bytedata, 0, numbytes);
                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                    bitmap.UnlockBits(bmpdata);
            }
        }
    }
}
