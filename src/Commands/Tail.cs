// Tail.cs — tail command: output the last part of files
using System;
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Tail
{
    public static void Run(int pid, string[] args)
    {
        int count = 10;
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "-n" && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed))
            {
                count = parsed;
                i++;
            }
            else if (a.StartsWith("-n") && int.TryParse(a[2..], out parsed)) count = parsed;
            else if (a.StartsWith('-') && a.Length > 1 && int.TryParse(a[1..], out parsed)) count = parsed;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"tail: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'tail --help' for more information.", ConsoleColor.Gray);
                PManager.Exit(pid, 1);
                return;
            }
            else files.Add(a);
        }

        if (files.Count == 0 || (files.Count == 1 && files[0] == "-"))
        {
            PrintTail(Output.GetStdin(), count);
            return;
        }

        foreach (string f in files)
        {
            string path = CManager.ResolvePath(f);
            if (!Cosmos.Kernel.System.Vfs.VfsManager.TryStat(path, out var st) || st.IsDirectory)
            {
                Output.WriteLine($"tail: '{f}': No such file or directory", ConsoleColor.Red);
                PManager.Exit(pid, 1);
                return;
            }
            if (files.Count > 1) Output.WriteLine($"==> {f} <==");
            PrintTail(CManager.ReadFileText(f), count);
        }
    }

    private static void PrintTail(string text, int count)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] raw = text.Split('\n');
        var lines = new List<string>();
        for (int i = 0; i < raw.Length; i++)
        {
            string line = raw[i].TrimEnd('\r');
            if (i == raw.Length - 1 && string.IsNullOrEmpty(line)) break;
            lines.Add(line);
        }

        int start = Math.Max(0, lines.Count - count);
        for (int i = start; i < lines.Count; i++)
        {
            Output.WriteLine(lines[i]);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: tail [OPTION]... [FILE]...", ConsoleColor.White);
        Output.WriteLine("Print the last 10 lines of each FILE to standard output.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -n NUM       output the last NUM lines instead of 10", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help   display this help and exit", ConsoleColor.Gray);
    }
}
