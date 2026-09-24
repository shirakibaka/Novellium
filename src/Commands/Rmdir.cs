// Rmdir.cs — rmdir command: remove empty directories
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Rmdir
{
    public static void Run(int pid, string[] args)
    {
        var dirs = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help") { Help(); return; }
            if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"rmdir: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'rmdir --help' for more information.", ConsoleColor.Gray);
                return;
            }
            dirs.Add(a);
        }

        if (dirs.Count == 0)
        {
            Output.WriteLine("usage: rmdir [OPTION]... DIRECTORY...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'rmdir --help' for more information.", ConsoleColor.Gray);
            return;
        }

        foreach (string dir in dirs)
        {
            string path = CManager.ResolvePath(dir);
            if (!VfsManager.TryStat(path, out VfsStat stat))
            {
                Output.WriteLine($"rmdir: failed to remove '{dir}': No such file or directory", ConsoleColor.Red);
                continue;
            }
            if (!stat.IsDirectory)
            {
                Output.WriteLine($"rmdir: failed to remove '{dir}': Not a directory", ConsoleColor.Red);
                continue;
            }
            if (!VfsManager.TryRemoveDirectory(path))
                Output.WriteLine($"rmdir: failed to remove '{dir}': Directory not empty or busy", ConsoleColor.Red);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: rmdir [OPTION]... DIRECTORY...", ConsoleColor.White);
        Output.WriteLine("Remove the DIRECTORY(ies), if they are empty.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
