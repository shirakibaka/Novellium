// Touch.cs — touch command: create empty files or update timestamps
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Touch
{
    public static void Run(int pid, string[] args)
    {
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help") { Help(); return; }
            if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"touch: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'touch --help' for more information.", ConsoleColor.Gray);
                return;
            }
            files.Add(a);
        }

        if (files.Count == 0)
        {
            Output.WriteLine("usage: touch [OPTION]... FILE...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'touch --help' for more information.", ConsoleColor.Gray);
            return;
        }

        foreach (string file in files)
        {
            string path = CManager.ResolvePath(file);
            if (VfsManager.TryStat(path, out _)) continue;
            if (!VfsManager.TryCreateFile(path, (VfsMode)420))
                Output.WriteLine($"touch: cannot touch '{file}': Failed to create file", ConsoleColor.Red);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: touch [OPTION]... FILE...", ConsoleColor.White);
        Output.WriteLine("Update the access and modification times of each FILE, or create it if non-existent.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
