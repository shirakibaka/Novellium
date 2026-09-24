// Rm.cs — rm command: remove files
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Rm
{
    public static void Run(int pid, string[] args)
    {
        bool force = false;
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-f" or "--force") force = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"rm: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'rm --help' for more information.", ConsoleColor.Gray);
                return;
            }
            else files.Add(a);
        }

        if (files.Count == 0)
        {
            Output.WriteLine("usage: rm [OPTION]... FILE...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'rm --help' for more information.", ConsoleColor.Gray);
            return;
        }

        foreach (string file in files)
        {
            string path = CManager.ResolvePath(file);
            if (!VfsManager.TryStat(path, out VfsStat stat))
            {
                if (!force) Output.WriteLine($"rm: cannot remove '{file}': No such file or directory", ConsoleColor.Red);
                continue;
            }
            if (stat.IsDirectory)
            {
                Output.WriteLine($"rm: cannot remove '{file}': Is a directory", ConsoleColor.Red);
                continue;
            }
            if (!VfsManager.TryUnlink(path))
                Output.WriteLine($"rm: cannot remove '{file}': Operation failed", ConsoleColor.Red);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: rm [OPTION]... FILE...", ConsoleColor.White);
        Output.WriteLine("Remove (unlink) the FILE(s).", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -f, --force   ignore nonexistent files and arguments, never prompt", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
