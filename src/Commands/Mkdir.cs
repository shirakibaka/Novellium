// Mkdir.cs — mkdir command: create new directories
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Mkdir
{
    public static void Run(int pid, string[] args)
    {
        bool parents = false;
        var dirs = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-p" or "--parents") parents = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"mkdir: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'mkdir --help' for more information.", ConsoleColor.Gray);
                return;
            }
            else dirs.Add(a);
        }

        if (dirs.Count == 0)
        {
            Output.WriteLine("usage: mkdir [OPTION]... DIRECTORY...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'mkdir --help' for more information.", ConsoleColor.Gray);
            return;
        }

        foreach (string dir in dirs)
        {
            string path = CManager.ResolvePath(dir);
            if (parents) MakeParents(path);
            else
            {
                if (VfsManager.TryStat(path, out _))
                {
                    Output.WriteLine($"mkdir: cannot create directory '{dir}': File exists", ConsoleColor.Red);
                    continue;
                }
                if (!VfsManager.TryCreateDirectory(path, (VfsMode)493))
                    Output.WriteLine($"mkdir: cannot create directory '{dir}': Failed", ConsoleColor.Red);
            }
        }
    }

    private static void MakeParents(string path)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string cur = "";
        foreach (string part in parts)
        {
            cur += "/" + part;
            if (VfsManager.TryStat(cur, out var stat))
            {
                if (!stat.IsDirectory)
                {
                    Output.WriteLine($"mkdir: cannot create directory '{path}': Not a directory", ConsoleColor.Red);
                    return;
                }
                continue;
            }
            if (!VfsManager.TryCreateDirectory(cur, (VfsMode)493))
            {
                Output.WriteLine($"mkdir: cannot create directory '{cur}': Failed", ConsoleColor.Red);
                return;
            }
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: mkdir [OPTION]... DIRECTORY...", ConsoleColor.White);
        Output.WriteLine("Create the DIRECTORY(ies), if they do not already exist.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -p, --parents  no error if existing, make parent directories as needed", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help     display this help and exit", ConsoleColor.Gray);
    }
}
