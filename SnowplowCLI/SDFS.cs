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
            packageCount = stream.ReadUInt32(); //count of sdfdata archives
            ddsHeaderCount = stream.ReadUInt32();
            ID startId = new ID(stream);

            byte flag = stream.ReadByte();
            if (flag != 0)
            {
                byte[] unk1 = stream.ReadBytes(0x140); //no idea whats contained in these bytes
            }

            if (dataOffset != 0)
            {
                stream.Position = dataOffset + 0x51; //idk what the extra 81 bytes is for but it takes us straight to the toc block
            }
            else
            {
                uint[] unk2 = new uint[packageCount]; //unknown, probably related to the packages in some way given the count matches the package count
                for (int i = 0;i < packageCount; i++)
                {
                    unk2[i] = stream.ReadUInt32();
                }

                ID[] packageIds = new ID[packageCount]; //read package ids
                for (int i = 0;i < packageCount; i++)
                {
                    packageIds[i] = new ID(stream);
                }

                DDSHeader[] ddsHeaders = new DDSHeader[ddsHeaderCount]; //read dds headers
                for (int i = 0; i < ddsHeaderCount; i++)
                {
                    ddsHeaders[i] = new DDSHeader(stream);
                }
            }

            uint signature = stream.ReadUInt32();
            stream.Position -= 4; //go back to start of compressed data
            compressedToc = stream.ReadBytes((int)compressedTocSize);
            decompressedToc = DecompressTocBlock(signature, compressedToc, decompressedTocSize, version);
            ID endId = new ID(stream);
        }

        public byte[] DecompressTocBlock(uint signature, byte[] compressedToc, uint decompressedTocSize, uint version)
        {
            byte[] DecompressedToc = new byte[decompressedTocSize];
            if (signature == 0xDFF25B82 || signature == 0xFD2FB528) //zstd
            {
                DecompressedToc = ZstdUtils.Decompressor(compressedToc);
                return DecompressedToc;
            }
            else if (signature == 0x184D2204 || version >= 0x17) //lz4
            {
                Console.WriteLine("LZ4 Compression not implimented.");
                return DecompressedToc;
            }
            else //zlib
            {
                Console.WriteLine("ZLIB Compression not implimented.");
                return DecompressedToc;
            }

        }

        public class ID
        {
            public string massive;
            public byte[] checksum;
            public string ubisoft;

            public ID(DataStream stream)
            {
                massive = stream.ReadNullTerminatedString();
                checksum = stream.ReadBytes(0x20); //unsure what method this uses
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
                data = stream.ReadBytes(200); //approximation of the dds header size. hopefully this works
            }
        }

    }

}
