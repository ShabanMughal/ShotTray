using System;
using System.Diagnostics;
using System.IO;

namespace ShotTray
{
    // `shot` command-line helper: hand the latest screenshot path to a terminal / CLI tool.
    static class Cli
    {
        static int Main(string[] args)
        {
            string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "last";
            FileInfo[] files;

            switch (cmd)
            {
                case "last":
                    files = Store.Recent();
                    if (files.Length == 0)
                    {
                        Console.Error.WriteLine("No screenshots yet. Press Ctrl+Shift+4 to take one.");
                        return 1;
                    }
                    Console.WriteLine(files[0].FullName);
                    return 0;

                case "list":
                    int count = 10;
                    if (args.Length > 1 && !int.TryParse(args[1], out count))
                    {
                        Console.Error.WriteLine("Usage: shot list [count]");
                        return 2;
                    }
                    files = Store.Recent();
                    for (int i = 0; i < files.Length && i < count; i++)
                        Console.WriteLine(files[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + "  " + files[i].FullName);
                    return 0;

                case "dir":
                    Console.WriteLine(Store.Folder);
                    return 0;

                case "open":
                    Directory.CreateDirectory(Store.Folder);
                    Process.Start("explorer.exe", "\"" + Store.Folder + "\"");
                    return 0;

                case "help":
                case "-h":
                case "--help":
                case "/?":
                    PrintUsage();
                    return 0;

                default:
                    Console.Error.WriteLine("Unknown command: " + args[0]);
                    PrintUsage();
                    return 2;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("shot - ShotTray screenshot helper");
            Console.WriteLine();
            Console.WriteLine("  shot            print path of the latest screenshot (same as 'shot last')");
            Console.WriteLine("  shot list [n]   list the n most recent screenshots (default 10)");
            Console.WriteLine("  shot dir        print the screenshots folder");
            Console.WriteLine("  shot open       open the screenshots folder in Explorer");
            Console.WriteLine();
            Console.WriteLine("Screenshots older than " + Store.KeepDays + " days are deleted automatically.");
        }
    }
}
