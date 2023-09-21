using SnowplowCLI.Utils;
using SnowplowCLI.Utils.Compression;

namespace SnowplowCLI
{
    public class SDFS
    {
        public uint decompressedFileTableSize;
        public uint dataOffset;
        public uint compressedFileTableSize;
        public uint firstInstallPart;
        public uint installPartCount;
        public uint[] installPartSizes;
        public uint ddsHeaderCount;
        public DDSHeader[] ddsHeaders;
        public FileTable fileTable;

        public void Initalise(DataStream stream, uint version, string seperator)
        {
            //
            //initalises the file system
            //
            decompressedFileTableSize = stream.ReadUInt32();
            if (version >= 0x17)
                dataOffset = stream.ReadUInt32();
            compressedFileTableSize = stream.ReadUInt32();
            firstInstallPart = stream.ReadUInt32();
            installPartCount = stream.ReadUInt32(); //count of sdfdata archives
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
                installPartSizes = new uint[installPartCount];
                for (int i = 0;i < installPartCount; i++)
                {
                    installPartSizes[i] = stream.ReadUInt32();
                }

                ID[] installPartIds = new ID[installPartCount]; //read installPart ids
                for (int i = 0;i < installPartCount; i++)
                {
                    installPartIds[i] = new ID(stream);
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
            fileTable = ReadFileTable(signature, compressedFileTable, decompressedFileTableSize, version, seperator);
            ID endId = new ID(stream);

        }

        public byte[] RequestFileData(SDFS fs, FileEntry fileEntry, string path)
        {
            List<byte[]> fileData = new List<byte[]>();

            string installPartPath = Path.Combine(path, fileEntry.installPartName);
            if (File.Exists(installPartPath))
            {
                using (DataStream stream = BlockStream.FromFile(installPartPath))
                {
                    if (fileEntry.compressedSizes.Count == 0)
                    {
                        stream.Position = (long)fileEntry.filePartoffset;
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
                                stream.Seek((int)fileEntry.filePartoffset + compOffset, SeekOrigin.Begin);
                                fileEntry.compressedSizes[i] = (ulong)decompressedSize;
                                fileData.Add(stream.ReadBytes(decompressedSize));
                            }
                            else
                            {
                                stream.Seek((int)fileEntry.filePartoffset + compOffset, SeekOrigin.Begin);
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

        public FileTable ReadFileTable(uint signature, byte[] compressedFileTable, uint decompressedFileTableSize, uint version, string seperator)
        {
            //
            //calls to decompress the file table and then passes it to the parser
            //
            byte[] decompressedFileTable = DecompressFileTable(signature, compressedFileTable, decompressedFileTableSize, version); //decompress the file table
            MemoryStream stream = new MemoryStream(decompressedFileTable); //convert to stream
            
            using (DataStream stream1 = new DataStream(stream))
            {
                FileTable fileTable = new FileTable();
                ParseFileTable(stream1, fileTable, version, seperator); //parse!
                return fileTable;
            }         
        }

        public void ParseFileTable(DataStream stream, FileTable fileTable, uint version, string seperator, string name = "")
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

                ParseFileTable(stream, fileTable, version, seperator, name);
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
                    ulong ddsType = ReadVariadicInteger(byteCount, stream);

                    for (int chunkIndex = 0; chunkIndex < count1; chunkIndex++)
                    {
                        byte ch3 = stream.ReadByte();
                        // if (ch3 == 0)
                        //    {
                        //        break;
                        //    }

                        int compressedSizeByteCount = (ch3 & 3) + 1;
                        int filePartOffsetByteCount = (ch3 >> 2) & 7;
                        bool hasCompression = ((ch3 >> 5) & 1) != 0;

                        ulong decompressedSize = 0;
                        ulong compressedSize = 0;
                        ulong filePartOffset = 0;
                        long fileId = -1;

                        if (compressedSizeByteCount > 0)
                        {
                            decompressedSize = ReadVariadicInteger(compressedSizeByteCount, stream);
                        }
                        if (hasCompression)
                        {
                            compressedSize = ReadVariadicInteger(compressedSizeByteCount, stream);
                        }
                        if (filePartOffsetByteCount != 0)
                        {
                            filePartOffset = ReadVariadicInteger(filePartOffsetByteCount, stream);
                        }

                        ulong installPartId = ReadVariadicInteger(2, stream);


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

                        AddFileEntry(fileTable.fileEntries, name, installPartId, seperator, filePartOffset, hasCompression, compSizeArray, decompressedSize, byteCount != 0 && chunkIndex == 0, ddsType, chunkIndex != 0);
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
                ParseFileTable(stream, fileTable, version, seperator, name);
                stream.Seek(offset, SeekOrigin.Begin);
                ParseFileTable(stream, fileTable, version, seperator, name);
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

        public void AddFileEntry(List<FileEntry> fileEntries, string fileName, ulong installPartId, string seperator, ulong filePartoffset, bool isCompressed, List<ulong> compressedSizes, ulong decompressedSize, bool isDDS, ulong ddsHeaderIndex, bool isChunk)
        {
            //
            //adds a file entry to the file table
            //
            string installPartName = GetinstallPartName(installPartId, seperator);
            fileEntries.Add(new FileEntry()
            {
                fileName = fileName,
                installPartName = installPartName,
                filePartoffset = filePartoffset,
                isCompressed = isCompressed,
                compressedSizes = compressedSizes,
                decompressedSize = decompressedSize,
                isDDS = isDDS,
                ddsHeaderIndex = ddsHeaderIndex,
                isChunk = isChunk
            });
        }

        public string GetinstallPartName(ulong installPartId, string seperator)
        {
            //
            //get sdfdata installPart name for a specified installPartId
            //
            string installPartLayer;
            if (installPartId < 1000) installPartLayer = "A";
            else if (installPartId < 2000) installPartLayer = "B";
            else if (installPartId < 3000) installPartLayer = "C";
            else installPartLayer = "D";

            string installPartName = $"sdf{seperator}{installPartLayer}{seperator}{installPartId.ToString("D" + 4)}.sdfdata";
            return installPartName;
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
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
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
            public string installPartName;
            public ulong filePartoffset;
            public bool isCompressed;
            public List<ulong> compressedSizes;
            public ulong decompressedSize;
            public bool isDDS;
            public ulong ddsHeaderIndex;
            public bool isChunk;
        }

        public class ID
        {
            public string massive;
            public byte[] data;
            public string ubisoft;

            public ID(DataStream stream)
            {
                massive = stream.ReadNullTerminatedString();
                data = stream.ReadBytes(0x20); //unsure what method this uses
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
