using SnowplowCLI.Utils;
using System.IO;
using System.IO.Enumeration;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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
                        SDFS fs = new SDFS();
                        fs.Initalise(stream, version);

                        foreach (FileEntry fileEntry in fs.fileTable.fileEntries)
                        {
                            byte[] fileData = fs.RequestFileData(fs, fileEntry, installDir);
                            string fsFileDir = Path.GetDirectoryName(fileEntry.fileName);
                            string outputPath = Path.Combine(dumpPath, fsFileDir);
                            Directory.CreateDirectory(outputPath);
                            var writer = new BinaryWriter(File.Create(Path.Combine(dumpPath, fileEntry.fileName)));
                            writer.Write(fileData);
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