using SnowplowCLI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace SnowplowCLI
{
    public class SDFS
    {
        public uint decompressedSize;
        public uint dataOffset;
        public uint compressedSize;
        public uint idCount;
        public uint ddsHeaderCount;

        public void Read(DataStream stream, uint version)
        {
            decompressedSize = stream.ReadUInt32();
            if (version >= 0x17)
                dataOffset = stream.ReadUInt32();
            compressedSize = stream.ReadUInt32();
            stream.Position += 4; //pad
            idCount = stream.ReadUInt32();
            ddsHeaderCount = stream.ReadUInt32();
            var startId = new ID(stream);

            byte flag = stream.ReadByte();
            if (flag != 0)
            {
                byte[] unk = stream.ReadBytes(0x140);
            }

            if (dataOffset != 0)
            {
                stream.Position = dataOffset + 0x51;

                uint signature = stream.ReadUInt32();
                byte[] compressedToc = stream.ReadBytes((int)compressedSize);
                DecompressTocBlock(signature, compressedToc, decompressedSize, version);
                var endId = new ID(stream);
            }
            else
            {
                //todo
            }
        }

        public void DecompressTocBlock(uint signature, byte[] CompressedToc, uint decompressedSize, uint version)
        {
            byte[] DecompressedToc = null;
            if (signature == 0xDFF25B82 || signature == 0xFD2FB528)
            {
                DecompressedToc = ZstdUtils.Decompressor(CompressedToc);
            }
            else if (signature == 0x184D2204 || version >= 0x17)
            {
                Console.WriteLine("LZ4 Compression not implimented.");
                return;
            }
            else
            {
                Console.WriteLine("ZLIB Compression not implimented.");
                return;
            }

        }

        public class ID
        {
            public string massive;
            public byte[] data;
            public string ubisoft;

            public ID(DataStream stream)
            {
                massive = stream.ReadNullTerminatedString();
                data = stream.ReadBytes(0x20);
                ubisoft = stream.ReadNullTerminatedString();
            }
        }

    }

}
