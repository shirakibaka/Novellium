// Tree.cs — tree command: list contents of directories in a tree-like format
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Tree
{
    private struct Node
    {
        public string Name;
        public string Path;
        public VfsStat Stat;
    }

    public static void Run(int pid, string[] args)
    {
        string targetPath = ".";
        int maxLevel = 100;
        bool dirsOnly = false;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "-L" && i + 1 < args.Length && int.TryParse(args[i + 1], out int lvl))
            {
                maxLevel = lvl;
                i++;
            }
            else if (a == "-d") dirsOnly = true;
            else if (!a.StartsWith('-') && targetPath == ".")
            {
                targetPath = a;
            }
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"tree: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'tree --help' for more information.", ConsoleColor.Gray);
                return;
            }
        }

        string fullPath = CManager.ResolvePath(targetPath);
        if (!VfsManager.TryStat(fullPath, out VfsStat rootStat))
        {
            Output.WriteLine($"tree: '{targetPath}': No such file or directory", ConsoleColor.Red);
            return;
        }

        Output.WriteLine(targetPath, rootStat.IsDirectory ? ConsoleColor.Cyan : ConsoleColor.White);
        if (!rootStat.IsDirectory) return;

        int dirCount = 0;
        int fileCount = 0;

        PrintTree(fullPath, "", 1, maxLevel, dirsOnly, ref dirCount, ref fileCount);

        Output.WriteLine();
        if (dirsOnly) Output.WriteLine($"{dirCount} directories", ConsoleColor.Gray);
        else Output.WriteLine($"{dirCount} directories, {fileCount} files", ConsoleColor.Gray);
    }

    private static void PrintTree(string path, string indent, int currentLevel, int maxLevel, bool dirsOnly, ref int dirCount, ref int fileCount)
    {
        if (currentLevel > maxLevel) return;

        if (!VfsManager.TryOpenDirectory(path, out var dir) || dir == null) return;

        IReadOnlyList<IVfsInode>? entries = null;
        using (dir)
        {
            dir.TryReadDir(out entries);
        }

        if (entries == null) return;

        var list = new List<Node>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.Name) || e.Name == "." || e.Name == "..") continue;

            string childPath = path == "/" ? "/" + e.Name : path + "/" + e.Name;
            if (!VfsManager.TryStat(childPath, out VfsStat st)) continue;

            if (dirsOnly && !st.IsDirectory) continue;

            list.Add(new Node { Name = e.Name, Path = childPath, Stat = st });
        }

        // Sort entries deterministically by name
        for (int i = 0; i < list.Count - 1; i++)
        {
            for (int j = 0; j < list.Count - 1 - i; j++)
            {
                if (string.CompareOrdinal(list[j].Name, list[j + 1].Name) > 0)
                {
                    (list[j], list[j + 1]) = (list[j + 1], list[j]);
                }
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            bool isLast = (i == list.Count - 1);
            Node node = list[i];

            string connector = isLast ? "`-- " : "|-- ";
            Output.Write(indent + connector, ConsoleColor.Gray);
            Output.WriteLine(node.Name, node.Stat.IsDirectory ? ConsoleColor.Cyan : ConsoleColor.White);

            if (node.Stat.IsDirectory)
            {
                dirCount++;
                string nextIndent = indent + (isLast ? "    " : "|   ");
                PrintTree(node.Path, nextIndent, currentLevel + 1, maxLevel, dirsOnly, ref dirCount, ref fileCount);
            }
            else
            {
                fileCount++;
            }
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: tree [PATH] [OPTIONS]", ConsoleColor.White);
        Output.WriteLine("List contents of directories in a tree-like format.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -L level      max display depth of the directory tree", ConsoleColor.Gray);
        Output.WriteLine("  -d            list directories only", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
