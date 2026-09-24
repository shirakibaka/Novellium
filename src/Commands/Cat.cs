// Cat.cs — cat command: display file contents and concatenate files
using System;
using System.Collections.Generic;
using System.Text;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

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
            if (a is "-h" or "--help") { Help(); return; }
            if (a is "-n" or "--number") num = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"cat: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'cat --help' for more information.", ConsoleColor.Gray);
                return;
            }
            else files.Add(a);
        }

        if (files.Count == 0)
        {
            Output.WriteLine("usage: cat [OPTION]... [FILE]...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'cat --help' for more information.", ConsoleColor.Gray);
            return;
        }

        int line = 1;
        foreach (string f in files)
        {
            string path = CManager.ResolvePath(f);
            if (!VfsManager.TryStat(path, out VfsStat st))
            {
                Output.WriteLine($"cat: {f}: No such file or directory", ConsoleColor.Red);
                continue;
            }
            if (st.IsDirectory)
            {
                Output.WriteLine($"cat: {f}: Is a directory", ConsoleColor.Red);
                continue;
            }
            if (!VfsManager.TryOpenFile(path, out var h) || h == null)
            {
                Output.WriteLine($"cat: {f}: Cannot open file", ConsoleColor.Red);
                continue;
            }

            using (h)
            {
                byte[] buf = new byte[1024];
                StringBuilder sb = new();
                long read;
                while ((read = h.Read(buf)) > 0)
                    sb.Append(Encoding.UTF8.GetString(buf, 0, (int)read));

                string content = sb.ToString();
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
        }
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
