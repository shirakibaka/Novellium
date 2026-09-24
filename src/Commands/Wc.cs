// Wc.cs — wc command: print line, word, and byte counts
using System;
using System.Collections.Generic;
using System.Text;
using Novellium.IO;

namespace Novellium.Commands;

public static class Wc
{
    public static void Run(int pid, string[] args)
    {
        bool linesOnly = false, wordsOnly = false, bytesOnly = false;
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "-l") linesOnly = true;
            else if (a == "-w") wordsOnly = true;
            else if (a == "-c") bytesOnly = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"wc: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'wc --help' for more information.", ConsoleColor.Gray);
                return;
            }
            else files.Add(a);
        }

        if (!linesOnly && !wordsOnly && !bytesOnly)
        {
            linesOnly = wordsOnly = bytesOnly = true;
        }

        if (files.Count == 0 || (files.Count == 1 && files[0] == "-"))
        {
            CountAndPrint(Output.GetStdin(), label: null, linesOnly, wordsOnly, bytesOnly);
            return;
        }

        int totLines = 0, totWords = 0, totBytes = 0;
        foreach (string f in files)
        {
            string text = CManager.ReadFileText(f);
            var (l, w, b) = CountText(text);
            totLines += l; totWords += w; totBytes += b;
            PrintCounts(l, w, b, f, linesOnly, wordsOnly, bytesOnly);
        }

        if (files.Count > 1)
        {
            PrintCounts(totLines, totWords, totBytes, "total", linesOnly, wordsOnly, bytesOnly);
        }
    }

    private static (int lines, int words, int bytes) CountText(string text)
    {
        if (string.IsNullOrEmpty(text)) return (0, 0, 0);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        string[] rawLines = text.Split('\n');
        int lineCount = rawLines.Length;
        if (text.EndsWith('\n')) lineCount--;

        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return (lineCount, words.Length, bytes.Length);
    }

    private static void CountAndPrint(string text, string? label, bool l, bool w, bool b)
    {
        var (lines, words, bytes) = CountText(text);
        PrintCounts(lines, words, bytes, label, l, w, b);
    }

    private static void PrintCounts(int lines, int words, int bytes, string? label, bool showL, bool showW, bool showB)
    {
        StringBuilder sb = new();
        if (showL) sb.Append($"{lines,8} ");
        if (showW) sb.Append($"{words,8} ");
        if (showB) sb.Append($"{bytes,8} ");
        if (label != null) sb.Append(label);
        Output.WriteLine(sb.ToString());
    }

    public static void Help()
    {
        Output.WriteLine("Usage: wc [OPTION]... [FILE]...", ConsoleColor.White);
        Output.WriteLine("Print newline, word, and byte counts for each FILE.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -l           print the newline counts", ConsoleColor.Gray);
        Output.WriteLine("  -w           print the word counts", ConsoleColor.Gray);
        Output.WriteLine("  -c           print the byte counts", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help   display this help and exit", ConsoleColor.Gray);
    }
}
