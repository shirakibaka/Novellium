// Grep.cs — grep command: print lines matching a pattern
using System;
using System.Collections.Generic;
using Novellium.IO;

namespace Novellium.Commands;

public static class Grep
{
    public static void Run(int pid, string[] args)
    {
        bool ignoreCase = false;
        bool invert = false;
        bool lineNum = false;
        string? pattern = null;
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-i" or "--ignore-case") ignoreCase = true;
            else if (a is "-v" or "--invert-match") invert = true;
            else if (a is "-n" or "--line-number") lineNum = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"grep: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'grep --help' for more information.", ConsoleColor.Gray);
                return;
            }
            else if (pattern == null) pattern = a;
            else files.Add(a);
        }

        if (pattern == null)
        {
            Output.WriteLine("usage: grep [OPTION]... PATTERN [FILE]...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'grep --help' for more information.", ConsoleColor.Gray);
            return;
        }

        StringComparison comp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (files.Count == 0 || (files.Count == 1 && files[0] == "-"))
        {
            string stdin = Output.GetStdin();
            ProcessText(stdin, pattern, comp, invert, lineNum, prefix: null);
            return;
        }

        bool showPrefix = files.Count > 1;
        foreach (string f in files)
        {
            string text = CManager.ReadFileText(f);
            ProcessText(text, pattern, comp, invert, lineNum, prefix: showPrefix ? f : null);
        }
    }

    private static void ProcessText(string text, string pattern, StringComparison comp, bool invert, bool lineNum, string? prefix)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (i == lines.Length - 1 && string.IsNullOrEmpty(line)) break;

            bool matches = line.Contains(pattern, comp);
            if (invert) matches = !matches;

            if (matches)
            {
                string pref = prefix != null ? $"{prefix}:" : "";
                string numStr = lineNum ? $"{i + 1}:" : "";
                Output.WriteLine($"{pref}{numStr}{line}");
            }
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: grep [OPTION]... PATTERN [FILE]...", ConsoleColor.White);
        Output.WriteLine("Search for PATTERN in each FILE or standard input.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -i, --ignore-case   ignore case distinctions", ConsoleColor.Gray);
        Output.WriteLine("  -v, --invert-match  select non-matching lines", ConsoleColor.Gray);
        Output.WriteLine("  -n, --line-number   print line number with output lines", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help          display this help and exit", ConsoleColor.Gray);
    }
}
