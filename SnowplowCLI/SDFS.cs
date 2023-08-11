using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowplowCLI
{
    public class SDFS
    {
        public uint DecompressedSize;
        public uint DataOffset; //not in 0x16 but lets set it up anyway
        public uint CompressedSize;
        public uint idCount;
        public uint ddsHeaderCount;

        public void Read(NativeReader reader, uint version)
        {
            DecompressedSize = reader.ReadUInt();
            if (version >= 0x17)
                DataOffset = reader.ReadUInt();
            CompressedSize = reader.ReadUInt();
            reader.Position += 4; //pad
            idCount = reader.ReadUInt();
            ddsHeaderCount = reader.ReadUInt();
            var startId = new ID(reader);

            byte flag = reader.ReadByte();
            if (flag != 0)
            {
                byte[] unk = reader.ReadBytes(0x140);
            }

            if (DataOffset != 0)
            {
                reader.Position = DataOffset + 0x51;

                uint signature = reader.ReadUInt();
                //decompress name block 
                var endId = new ID(reader);
            }
            else
            {
                //todo
            }
        }
    }

    public class ID
    {
        public string ubisoft;
        public byte[] data;
        public string massive;

        public ID(NativeReader reader)
        {
            ubisoft = reader.ReadNullTerminatedString();
            data = reader.ReadBytes(0x20);
            massive = reader.ReadNullTerminatedString();
        }
    }
}
