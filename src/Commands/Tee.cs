// Tee.cs — tee command: read from standard input and write to standard output and files
using System;
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Tee
{
    public static void Run(int pid, string[] args)
    {
        bool append = false;
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-a" or "--append") append = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"tee: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'tee --help' for more information.", ConsoleColor.Gray);
                PManager.Exit(pid, 1);
                return;
            }
            else files.Add(a);
        }

        string stdin = Output.GetStdin();
        Output.Write(stdin);

        bool hasError = false;
        foreach (string f in files)
        {
            if (!CManager.WriteFileText(f, stdin, append))
            {
                Output.WriteLine($"tee: '{f}': Write error", ConsoleColor.Red);
                hasError = true;
            }
        }

        if (hasError) PManager.Exit(pid, 1);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: tee [OPTION]... [FILE]...", ConsoleColor.White);
        Output.WriteLine("Copy standard input to each FILE, and also to standard output.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -a, --append   append to the given FILEs, do not overwrite", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help     display this help and exit", ConsoleColor.Gray);
    }
}
