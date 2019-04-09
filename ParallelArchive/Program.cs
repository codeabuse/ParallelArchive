using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MathLib;

namespace ParallelArchive
{
    class Program
    {
        static MTArchive archive;
        static bool exit;
        static string help = "";

        static int Main(string[] args)
        {
            archive = new MTArchive("Temp", 4);
            if (args.Length == 3)
            {
                foreach (var line in args)
                {
                    line.ToLower();
                }

                switch (args[0])
                {
                    case "compress":
                        archive.BeginWork(args[1], args[2], System.IO.Compression.CompressionMode.Compress);
                        break;
                    case "decompress":
                        archive.BeginWork(args[1], args[2], System.IO.Compression.CompressionMode.Decompress);
                        break;
                }
            }
            else
            {
                ConsoleCommands();
            }
            Console.WriteLine("Program closing...");
            Thread.Sleep(500);
            return archive.WorkResult();
        }

        static void ConsoleCommands()
        {
            while (!exit)
            {
                string userInput = Console.ReadLine().ToLower();
                string[] commands = userInput.Split(' ');

                switch (commands[0])
                {
                    case "compress":
                        archive.BeginWork(commands[1], commands[2], System.IO.Compression.CompressionMode.Compress);
                        break;

                    case "decompress":
                        archive.BeginWork(commands[1], commands[2], System.IO.Compression.CompressionMode.Decompress);
                        break;

                    case "workfolder":
                        archive.SetWorkFolder(commands[1]);
                        break;

                    case "help":
                        Console.WriteLine(help);
                        break;

                    case "exit":
                        exit = true;
                        break;

                    default:
                        Console.WriteLine($"Unknown command \"{commands[0]}\"\nType help to show available commands list");
                        break;
                }
            }
        }
    }
}
