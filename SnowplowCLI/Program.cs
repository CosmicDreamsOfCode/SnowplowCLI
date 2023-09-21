using SnowplowCLI.Utils;
using static SnowplowCLI.SDFS;

namespace SnowplowCLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string tocPath = args[0];
            string dumpPath = args[1];
            string installDir = Path.GetDirectoryName(tocPath);

            if (!File.Exists(tocPath))
            {
                Console.WriteLine("File does not exist.");
                return;
            }

            using (DataStream stream = BlockStream.FromFile(tocPath))
            {
                string idCheck = stream.ReadFixedSizedString(4);
                if (idCheck == "WEST")
                {
                    uint version = stream.ReadUInt32();
                    if (version != 0x16) //i could support more but i can only test this one
                    {
                        Console.WriteLine("Unsupported version " + version + " Expected 0x16.");
                        return;
                    }
                    else
                    {
                        //we have a valid TOC, let's initalise the file system
                        string seperator = $"-";
                        if (Directory.EnumerateFiles(installDir, "sdf-*-*.sdfdata").Count() < 1)
                        {
                            seperator = $"_";
                        }
                        SDFS fs = new SDFS();
                        fs.Initalise(stream, version, seperator);

                        foreach (FileEntry fileEntry in fs.fileTable.fileEntries)
                        {
                            byte[] fileData = fs.RequestFileData(fs, fileEntry, installDir);
                            string fsFileDir = Path.GetDirectoryName(fileEntry.fileName);
                            string outputPath = Path.Combine(dumpPath, fsFileDir);
                            Directory.CreateDirectory(outputPath);
                            if (fileEntry.isChunk) //if the file is a chunk it means we need to append it instead in case the first chunk was already written
                            {
                                using (var fileStream = new FileStream(Path.Combine(dumpPath, fileEntry.fileName), FileMode.Append, FileAccess.Write))
                                {
                                    using (var writer = new BinaryWriter(fileStream))
                                    {
                                        writer.Write(fileData);
                                    }
                                }
                            }
                            else
                            {
                                using (var writer = new BinaryWriter(File.Create(Path.Combine(dumpPath, fileEntry.fileName))))
                                {
                                    writer.Write(fileData);
                                }
                            }

                            Console.WriteLine(fileEntry.fileName);
                        }

                        Console.WriteLine("Finished!");
                    }
                }
                else
                {
                    Console.WriteLine("Not a valid TOC.");
                    return;
                }
            }
        }
    }
}