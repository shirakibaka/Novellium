// Cat.cs — cat command: display file contents and concatenate files
using System;
using System.Collections.Generic;
using System.Text;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.IO.Cache;
using Novellium.Process;

namespace Novellium.Commands;

public static class Cat
{
    public static void Run(int pid, string[] args)
    {
        bool num = false;
        var files = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-n" or "--number") num = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"cat: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'cat --help' for more information.", ConsoleColor.Gray);
                PManager.Exit(pid, 1);
                return;
            }
            else files.Add(a);
        }

        int line = 1;
        if (files.Count == 0 || (files.Count == 1 && files[0] == "-"))
        {
            string stdin = Output.GetStdin();
            if (!num)
            {
                Output.Write(stdin);
                if (!stdin.EndsWith('\n')) Output.WriteLine();
            }
            else
            {
                string[] lines = stdin.Split('\n');
                for (int j = 0; j < lines.Length; j++)
                {
                    if (j == lines.Length - 1 && string.IsNullOrEmpty(lines[j])) break;
                    Output.WriteLine($"{line,6}  {lines[j]}");
                    line++;
                }
            }
            return;
        }

        bool hasError = false;
        foreach (string f in files)
        {
            string path = CManager.ResolvePath(f);
            if (!VfsManager.TryStat(path, out VfsStat st))
            {
                Output.WriteLine($"cat: {f}: No such file or directory", ConsoleColor.Red);
                hasError = true;
                continue;
            }
            if (st.IsDirectory)
            {
                Output.WriteLine($"cat: {f}: Is a directory", ConsoleColor.Red);
                hasError = true;
                continue;
            }

            string content;
            if (VfsCacheEngine.TryReadFile(path, out var cachedData) && cachedData != null)
            {
                content = Encoding.UTF8.GetString(cachedData);
            }
            else
            {
                if (!VfsManager.TryOpenFile(path, out var h) || h == null)
                {
                    Output.WriteLine($"cat: {f}: Cannot open file", ConsoleColor.Red);
                    hasError = true;
                    continue;
                }

                using (h)
                {
                    byte[] buf = new byte[(int)st.Size];
                    long read = h.Read(buf);
                    content = Encoding.UTF8.GetString(buf, 0, (int)read);
                }
            }

            if (!num)
            {
                Output.Write(content);
                if (!content.EndsWith('\n')) Output.WriteLine();
            }
            else
            {
                string[] lines = content.Split('\n');
                for (int j = 0; j < lines.Length; j++)
                {
                    if (j == lines.Length - 1 && string.IsNullOrEmpty(lines[j])) break;
                    Output.WriteLine($"{line,6}  {lines[j]}");
                    line++;
                }
            }
        }

        if (hasError) PManager.Exit(pid, 1);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: cat [OPTION]... [FILE]...", ConsoleColor.White);
        Output.WriteLine("Concatenate FILE(s) to standard output.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -n, --number  number all output lines", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
