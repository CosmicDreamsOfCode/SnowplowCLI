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
        public uint decompressedTocSize;
        public uint dataOffset;
        public uint compressedTocSize;
        public uint packageCount;
        public uint ddsHeaderCount;
        public byte[] compressedToc;
        public byte[] decompressedToc;

        public void Read(DataStream stream, uint version)
        {
            decompressedTocSize = stream.ReadUInt32();
            if (version >= 0x17)
                dataOffset = stream.ReadUInt32();
            compressedTocSize = stream.ReadUInt32();
            stream.Position += 4; //pad
            packageCount = stream.ReadUInt32();
            ddsHeaderCount = stream.ReadUInt32();
            ID startId = new ID(stream);

            byte flag = stream.ReadByte();
            if (flag != 0)
            {
                byte[] unk1 = stream.ReadBytes(0x140);
            }

            if (dataOffset != 0)
            {
                stream.Position = dataOffset + 0x51;
            }
            else
            {
                uint[] unk2 = new uint[packageCount];
                for (int i = 0;i < packageCount; i++)
                {
                    unk2[i] = stream.ReadUInt32();
                }

                ID[] packageIds = new ID[packageCount];
                for (int i = 0;i < packageCount; i++)
                {
                    packageIds[i] = new ID(stream);
                }

                DDSHeader[] ddsHeaders = new DDSHeader[ddsHeaderCount];
                for (int i = 0; i < ddsHeaderCount; i++)
                {
                    ddsHeaders[i] = new DDSHeader(stream);
                }
            }

            uint signature = stream.ReadUInt32();
            stream.Position -= 4;
            compressedToc = stream.ReadBytes((int)compressedTocSize);
            decompressedToc = DecompressTocBlock(signature, compressedToc, decompressedTocSize, version);
            ID endId = new ID(stream);
        }

        public byte[] DecompressTocBlock(uint signature, byte[] compressedToc, uint decompressedTocSize, uint version)
        {
            byte[] DecompressedToc = new byte[decompressedTocSize];
            if (signature == 0xDFF25B82 || signature == 0xFD2FB528)
            {
                DecompressedToc = ZstdUtils.Decompressor(compressedToc);
                return DecompressedToc;
            }
            else if (signature == 0x184D2204 || version >= 0x17)
            {
                Console.WriteLine("LZ4 Compression not implimented.");
                return DecompressedToc;
            }
            else
            {
                Console.WriteLine("ZLIB Compression not implimented.");
                return DecompressedToc;
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

        public class DDSHeader
        {
            public uint unk;
            public byte[] data;

            public DDSHeader(DataStream stream)
            {
                unk = stream.ReadUInt32();
                data = stream.ReadBytes(200); //approximation of a dds header size. hopefully this works
            }
        }

    }

}
