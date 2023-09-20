using SnowplowCLI.Utils;
using SnowplowCLI.Utils.Compression;
using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SnowplowCLI
{
    public class SDFS
    {
        public uint decompressedFileTableSize;
        public uint dataOffset;
        public uint compressedFileTableSize;
        public uint packageCount;
        public uint ddsHeaderCount;
        public DDSHeader[] ddsHeaders;
        public FileTable fileTable;

        public void Initalise(DataStream stream, uint version)
        {
            //
            //initalises the file system
            //
            decompressedFileTableSize = stream.ReadUInt32();
            if (version >= 0x17)
                dataOffset = stream.ReadUInt32();
            compressedFileTableSize = stream.ReadUInt32();
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

                ddsHeaders = new DDSHeader[ddsHeaderCount]; //read dds headers
                for (int i = 0; i < ddsHeaderCount; i++)
                {
                    ddsHeaders[i] = new DDSHeader(stream);
                }
            }

            uint signature = stream.ReadUInt32();
            stream.Position -= 4; //go back to start of compressed data
            byte[] compressedFileTable = stream.ReadBytes((int)compressedFileTableSize);
            fileTable = ReadFileTable(signature, compressedFileTable, decompressedFileTableSize, version);
            ID endId = new ID(stream);

        }

        public byte[] RequestFileData(SDFS fs, FileEntry fileEntry, string path)
        {
            List<byte[]> fileData = new List<byte[]>();

            string packagePath = Path.Combine(path, fileEntry.packageName);
            if (File.Exists(packagePath))
            {
                using (DataStream stream = BlockStream.FromFile(packagePath))
                {
                    if (fileEntry.compressedSizes.Count == 0)
                    {
                        stream.Position = (long)fileEntry.offset;
                        fileData.Add(stream.ReadBytes((int)fileEntry.decompressedSize).ToArray());
                    }
                    else
                    {
                        var pageSize = (double)0x10000;
                        var decompOffset = 0;
                        var compOffset = 0;

                        if (fileEntry.isDDS)
                        {
                            MemoryStream ms = new MemoryStream(fs.ddsHeaders[fileEntry.ddsHeaderIndex].data);
                            using (DataStream ds = new DataStream(ms))
                            {
                                ds.Position = 84;
                                bool IsDX10 = ds.ReadFixedSizedString(4) == "DX10";

                                if (IsDX10)
                                {
                                    fileData.Add(fs.ddsHeaders[fileEntry.ddsHeaderIndex].data.Take((int)0x94).ToArray());
                                }
                                else
                                {
                                    fileData.Add(fs.ddsHeaders[fileEntry.ddsHeaderIndex].data.Take((int)0x80).ToArray());
                                }
                            }
                        }
                        
                        for (var i = 0; i < fileEntry.compressedSizes.Count; i++)
                        {
                            var decompressedSize = (int)Math.Min((int)fileEntry.decompressedSize - decompOffset, pageSize);
                            if (fileEntry.compressedSizes[i] == 0 || decompressedSize == (int)fileEntry.compressedSizes[i])
                            {
                                stream.Seek((int)fileEntry.offset + compOffset, SeekOrigin.Begin);
                                fileEntry.compressedSizes[i] = (ulong)decompressedSize;
                                fileData.Add(stream.ReadBytes(decompressedSize));
                            }
                            else
                            {
                                stream.Seek((int)fileEntry.offset + compOffset, SeekOrigin.Begin);
                                fileData.Add(Zstd.Decompress(stream.ReadBytes((int)fileEntry.compressedSizes[i])));
                            }
                            decompOffset += decompressedSize;
                            compOffset += (int)fileEntry.compressedSizes[i];
                        }
                    }
                }
            }

            return CombineByteArray(fileData.ToArray());
        }

        #region File Table

        public FileTable ReadFileTable(uint signature, byte[] compressedFileTable, uint decompressedFileTableSize, uint version)
        {
            //
            //calls to decompress the file table and then passes it to the parser
            //
            byte[] decompressedFileTable = DecompressFileTable(signature, compressedFileTable, decompressedFileTableSize, version); //decompress the file table
            MemoryStream stream = new MemoryStream(decompressedFileTable); //convert to stream
            
            using (DataStream stream1 = new DataStream(stream))
            {
                FileTable fileTable = new FileTable();
                ParseFileTable(stream1, fileTable, version); //parse!
                return fileTable;
            }         
        }

        public void ParseFileTable(DataStream stream, FileTable fileTable, uint version, string name = "")
        {
            //
            //adapted from https://github.com/KillzXGaming/Switch-Toolbox/blob/master/File_Format_Library/FileFormats/Archives/SDF.cs#L366
            //
            char ch = stream.ReadChar();

            if (ch == 0)
                throw new Exception("Unexcepted byte in file tree");

            if (ch >= 1 && ch <= 0x1f) //string part
            {
                while (ch-- > 0)
                {
                    name += stream.ReadChar();
                }

                ParseFileTable(stream, fileTable, version, name);
            }
            else if (ch >= 'A' && ch <= 'Z') //file entry
            {
                int var = Convert.ToInt32(ch - 'A');

                ch = Convert.ToChar(var);
                int count1 = ch & 7;
                int flag1 = (ch >> 3) & 1;
                //   int flag1 = ch & 8;

                if (count1 > 0)
                {
                    uint strangeId = stream.ReadUInt32();
                    byte chr2 = stream.ReadByte();
                    int byteCount = chr2 & 3;
                    int byteValue = chr2 >> 2;
                    ulong DdsType = ReadVariadicInteger(byteCount, stream);

                    for (int chunkIndex = 0; chunkIndex < count1; chunkIndex++)
                    {
                        byte ch3 = stream.ReadByte();
                        // if (ch3 == 0)
                        //    {
                        //        break;
                        //    }

                        int compressedSizeByteCount = (ch3 & 3) + 1;
                        int packageOffsetByteCount = (ch3 >> 2) & 7;
                        bool hasCompression = ((ch3 >> 5) & 1) != 0;

                        ulong decompressedSize = 0;
                        ulong compressedSize = 0;
                        ulong packageOffset = 0;
                        long fileId = -1;

                        if (compressedSizeByteCount > 0)
                        {
                            decompressedSize = ReadVariadicInteger(compressedSizeByteCount, stream);
                        }
                        if (hasCompression)
                        {
                            compressedSize = ReadVariadicInteger(compressedSizeByteCount, stream);
                        }
                        if (packageOffsetByteCount != 0)
                        {
                            packageOffset = ReadVariadicInteger(packageOffsetByteCount, stream);
                        }

                        ulong packageId = ReadVariadicInteger(2, stream);


                        List<ulong> compSizeArray = new List<ulong>();

                        if (hasCompression)
                        {
                            ulong pageCount = (decompressedSize + 0xffff) >> 16;
                            //   var pageCount = NextMultiple(decompressedSize, 0x10000) / 0x10000;
                            if (pageCount > 1)
                            {
                                for (ulong page = 0; page < pageCount; page++)
                                {
                                    ulong compSize = ReadVariadicInteger(2, stream);
                                    compSizeArray.Add(compSize);
                                }
                            }
                        }

                        if (version < 0x16) //Unsure. Rabbids doesn't use it, newer versions don't. 
                        {
                            fileId = (long)ReadVariadicInteger(4, stream);
                        }

                        if (compSizeArray.Count == 0 && hasCompression)
                            compSizeArray.Add(compressedSize);

                        addFileEntry(fileTable.fileEntries, name, packageId, packageOffset, hasCompression, compSizeArray, decompressedSize, byteCount != 0 && chunkIndex == 0, DdsType);
                    }
                }
                if ((ch & 8) != 0) //flag1
                {
                    byte ch3 = stream.ReadByte();
                    while (ch3-- > 0)
                    {
                        stream.ReadByte();
                        stream.ReadByte();
                    }
                }
            }
            else
            {
                uint offset = stream.ReadUInt32();
                ParseFileTable(stream, fileTable, version, name);
                stream.Seek(offset, SeekOrigin.Begin);
                ParseFileTable(stream, fileTable, version, name);
            }

        }

        public byte[] DecompressFileTable(uint signature, byte[] compressedFileTable, uint decompressedFileTableSize, uint version)
        {
            //
            //checks compression type and decompresses the file table
            //
            byte[] decompressedFileTable = new byte[decompressedFileTableSize];
            if (signature == 0xDFF25B82 || signature == 0xFD2FB528) //zstd
            {
                decompressedFileTable = Zstd.Decompress(compressedFileTable);
                return decompressedFileTable;
            }
            else if (signature == 0x184D2204 || version >= 0x17) //lz4
            {
                decompressedFileTable = Lz4.Decompress(compressedFileTable);
                return decompressedFileTable;
            }
            else //zlib
            {
                decompressedFileTable = Zlib.Decompress(compressedFileTable);
                return decompressedFileTable;
            }

        }

        #endregion

        #region Utility Functions

        public void addFileEntry(List<FileEntry> fileEntries, string fileName, ulong packageId, ulong offset, bool isCompressed, List<ulong> compressedSizes, ulong decompressedSize, bool isDDS, ulong ddsHeaderIndex)
        {
            //
            //adds a file entry to the file table
            //
            string packageName = GetPackageName(packageId);
            fileEntries.Add(new FileEntry()
            {
                fileName = fileName,
                packageName = packageName,
                offset = offset,
                isCompressed = isCompressed,
                compressedSizes = compressedSizes,
                decompressedSize = decompressedSize,
                isDDS = isDDS,
                ddsHeaderIndex = ddsHeaderIndex,
            });
        }

        public string GetPackageName(ulong packageId)
        {
            //
            //get sdfdata package name for a specified packageId
            //
            string packageLayer;
            if (packageId < 1000) packageLayer = "A";
            else if (packageId < 2000) packageLayer = "B";
            else if (packageId < 3000) packageLayer = "C";
            else packageLayer = "D";

            string packageName = $"sdf-{packageLayer}-{packageId.ToString("D" + 4)}.sdfdata";
            return packageName;
        }

        private ulong ReadVariadicInteger(int Count, DataStream stream)
        {
            //
            //adapted from https://github.com/KillzXGaming/Switch-Toolbox/blob/master/File_Format_Library/FileFormats/Archives/SDF.cs#L228
            //
            ulong result = 0;

            for (int i = 0; i < Count; i++)
            {
                result |= (ulong)(stream.ReadByte()) << (i * 8);
            }
            return result;
        }
        public static byte[] CombineByteArray(params byte[][] arrays)
        {
            //
            //from https://github.com/KillzXGaming/Switch-Toolbox/blob/master/Switch_Toolbox_Library/Util/Util.cs#L155
            //
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }

        #endregion

        #region Classes

        public class FileTable
        {
            public List<FileEntry> fileEntries = new List<FileEntry>();
        }

        public class FileEntry
        {
            public string fileName;
            public string packageName;
            public ulong offset;
            public bool isCompressed;
            public List<ulong> compressedSizes;
            public ulong decompressedSize;
            public bool isDDS;
            public ulong ddsHeaderIndex;
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
        #endregion

    }

}
