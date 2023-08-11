using SnowplowCLI;
using System.IO;
using System.Reflection;

namespace SnowplowCLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //string filePath = args[0];
            //int index = args[0].LastIndexOf("\\");
            string filePath = @"I:\SwitchDumps\MarioRabbids\romfs\moria\sdf\nx\data\sdf.sdftoc";
            string fileDir = Path.GetFileName(filePath);
            string exepath = Assembly.GetExecutingAssembly().Location;
            exepath = Path.GetDirectoryName(exepath);

            if (!File.Exists(filePath))
            {
                Console.WriteLine("File does not exist.");
                return;
            }

            using (NativeReader reader = new (new FileStream(filePath, FileMode.Open)))
            {
                string idCheck = reader.ReadSizedString(4);
                if (idCheck == "WEST")
                {
                    uint version = reader.ReadUInt();
                    if (version != 0x16) //i only really know of this one rn
                    {
                        Console.WriteLine("Unsupported version " + version + " Expected 0x16.");
                        return;
                    }
                    else
                    {
                        //we have an toc we support so lets read it
                        SDFS SDFS = new SDFS();
                        SDFS.Read(reader, version);
                    }
                }
                else
                {
                    Console.WriteLine("Invalid SDFTOC. Are you sure the file exists?");
                    return;
                }
            }
        }
    }
}