// Ls.cs — ls command: list directory contents
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Ls
{
    private struct Entry
    {
        public string Name;
        public VfsStat Stat;
    }

    public static void Run(int pid, string[] args)
    {
        bool showAll = false, longFmt = false, onePerLine = false;
        List<(string norm, string orig)> paths = new();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help") { Help(); return; }

            if (a.StartsWith("--"))
            {
                if (a == "--all") showAll = true;
                else
                {
                    Output.WriteLine($"ls: unrecognized option '{a}'", ConsoleColor.Red);
                    Output.WriteLine("Try 'ls --help' for more information.", ConsoleColor.Gray);
                    return;
                }
            }
            else if (a.StartsWith('-') && a.Length > 1)
            {
                for (int j = 1; j < a.Length; j++)
                {
                    char c = a[j];
                    if (c == 'a') showAll = true;
                    else if (c == 'l') longFmt = true;
                    else if (c == '1') onePerLine = true;
                    else if (c == 'h') { Help(); return; }
                    else
                    {
                        Output.WriteLine($"ls: invalid option -- '{c}'", ConsoleColor.Red);
                        Output.WriteLine("Try 'ls --help' for more information.", ConsoleColor.Gray);
                        return;
                    }
                }
            }
            else paths.Add((CManager.ResolvePath(a), a));
        }

        if (paths.Count == 0)
            paths.Add((CManager.CurrentDirectory, CManager.CurrentDirectory));

        bool multi = paths.Count > 1;
        for (int i = 0; i < paths.Count; i++)
        {
            var (norm, orig) = paths[i];
            if (multi)
            {
                if (i > 0) Output.WriteLine();
                Output.WriteLine($"{orig}:", ConsoleColor.Gray);
            }
            ListDir(norm, orig, showAll, longFmt, onePerLine);
        }
    }

    private static void ListDir(string path, string orig, bool showAll, bool longFmt, bool onePerLine)
    {
        if (!VfsManager.TryStat(path, out VfsStat targetStat))
        {
            Output.WriteLine($"ls: cannot access '{orig}': No such file or directory", ConsoleColor.Red);
            return;
        }

        if (!targetStat.IsDirectory)
        {
            Print(FileName(orig), targetStat, longFmt);
            if (!longFmt) Output.WriteLine();
            return;
        }

        if (!VfsManager.TryOpenDirectory(path, out var dir) || dir == null)
        {
            Output.WriteLine($"ls: cannot open directory '{orig}'", ConsoleColor.Red);
            return;
        }

        using (dir)
        {
            if (!dir.TryReadDir(out var entries) || entries == null)
            {
                Output.WriteLine($"ls: failed to read directory '{orig}'", ConsoleColor.Red);
                return;
            }

            List<Entry> list = new();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.Name)) continue;
                if (!showAll && e.Name.StartsWith('.')) continue;

                string child = path == "/" ? "/" + e.Name : path + "/" + e.Name;
                VfsStat st = default;
                if (e.InodeOperations != null && e.InodeOperations.GetAttr(e, out st)) { }
                else VfsManager.TryStat(child, out st);

                list.Add(new Entry { Name = e.Name, Stat = st });
            }

            list.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));

            if (longFmt)
            {
                foreach (var item in list) Print(item.Name, item.Stat, true);
            }
            else if (onePerLine)
            {
                foreach (var item in list)
                {
                    PrintName(item.Name, item.Stat);
                    Output.WriteLine();
                }
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    PrintName(list[i].Name, list[i].Stat);
                    if (i < list.Count - 1) Output.Write("  ");
                }
                if (list.Count > 0) Output.WriteLine();
            }
        }
    }

    private static string FileName(string path)
    {
        int slash = path.LastIndexOf('/');
        return (slash >= 0 && slash < path.Length - 1) ? path.Substring(slash + 1) : path;
    }

    private static void PrintName(string name, VfsStat st)
        => Output.Write(name, st.IsDirectory ? ConsoleColor.Cyan : ConsoleColor.White);

    private static void Print(string name, VfsStat st, bool longFmt)
    {
        if (longFmt)
        {
            Output.Write($"{FmtMode(st)} {st.Size.ToString().PadLeft(8)} ", ConsoleColor.Gray);
            PrintName(name, st);
            Output.WriteLine();
        }
        else PrintName(name, st);
    }

    public static string FmtMode(VfsStat st)
    {
        int m = (int)st.Mode;
        return new string([
            st.IsDirectory ? 'd' : (st.IsSymbolicLink ? 'l' : '-'),
            (m & 0x100) != 0 ? 'r' : '-', (m & 0x080) != 0 ? 'w' : '-', (m & 0x040) != 0 ? 'x' : '-',
            (m & 0x020) != 0 ? 'r' : '-', (m & 0x010) != 0 ? 'w' : '-', (m & 0x008) != 0 ? 'x' : '-',
            (m & 0x004) != 0 ? 'r' : '-', (m & 0x002) != 0 ? 'w' : '-', (m & 0x001) != 0 ? 'x' : '-'
        ]);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: ls [OPTION]... [FILE]...", ConsoleColor.White);
        Output.WriteLine("List information about the FILEs (the root directory / by default).", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -a, --all     do not ignore entries starting with .", ConsoleColor.Gray);
        Output.WriteLine("  -l            use a long listing format", ConsoleColor.Gray);
        Output.WriteLine("  -1            list one file per line", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
