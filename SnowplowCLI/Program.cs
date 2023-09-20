﻿using SnowplowCLI.Utils;
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

            using (DataStream stream = BlockStream.FromFile(filePath))
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
                        SDFS SDFS = new SDFS();
                        SDFS.Initalise(stream, version);

                        byte[] testFile = SDFS.RequestFileData(SDFS, SDFS.fileTable.fileEntries[3290], @"I:\SwitchDumps\MarioRabbids\romfs\moria\sdf\nx\data\");
                        using var writer = new BinaryWriter(File.Create(@"I:\SwitchDumps\MarioRabbids\romfs\moria\sdf\nx\data\" + "her_mario_01_body_d.dds"));
                        writer.Write(testFile);
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