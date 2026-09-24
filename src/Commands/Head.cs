// Head.cs — head command: output the first part of files
using System;
using System.Collections.Generic;
using Novellium.IO;

namespace Novellium.Commands;

public static class Head
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
                Output.WriteLine($"head: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'head --help' for more information.", ConsoleColor.Gray);
                return;
            }
            else files.Add(a);
        }

        if (files.Count == 0 || (files.Count == 1 && files[0] == "-"))
        {
            PrintHead(Output.GetStdin(), count);
            return;
        }

        foreach (string f in files)
        {
            if (files.Count > 1) Output.WriteLine($"==> {f} <==");
            PrintHead(CManager.ReadFileText(f), count);
        }
    }

    private static void PrintHead(string text, int count)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] lines = text.Split('\n');
        int printed = 0;
        for (int i = 0; i < lines.Length && printed < count; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (i == lines.Length - 1 && string.IsNullOrEmpty(line)) break;
            Output.WriteLine(line);
            printed++;
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: head [OPTION]... [FILE]...", ConsoleColor.White);
        Output.WriteLine("Print the first 10 lines of each FILE to standard output.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -n NUM       print the first NUM lines instead of 10", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help   display this help and exit", ConsoleColor.Gray);
    }
}
