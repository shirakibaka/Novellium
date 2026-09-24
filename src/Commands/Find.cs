// Find.cs — find command: search for files in a directory hierarchy
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Find
{
    public static void Run(int pid, string[] args)
    {
        string targetPath = ".";
        string? namePattern = null;
        char? typeFilter = null;
        int maxDepth = 100;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "-name" && i + 1 < args.Length)
            {
                namePattern = args[++i];
            }
            else if (a == "-type" && i + 1 < args.Length)
            {
                string t = args[++i];
                if (t == "f") typeFilter = 'f';
                else if (t == "d") typeFilter = 'd';
            }
            else if (a == "-maxdepth" && i + 1 < args.Length && int.TryParse(args[i + 1], out int depth))
            {
                maxDepth = depth;
                i++;
            }
            else if (!a.StartsWith('-') && targetPath == ".")
            {
                targetPath = a;
            }
            else if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"find: unknown predicate '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'find --help' for more information.", ConsoleColor.Gray);
                return;
            }
        }

        string fullPath = CManager.ResolvePath(targetPath);
        if (!VfsManager.TryStat(fullPath, out _))
        {
            Output.WriteLine($"find: '{targetPath}': No such file or directory", ConsoleColor.Red);
            return;
        }

        Traverse(fullPath, targetPath, 0, maxDepth, namePattern, typeFilter);
    }

    private static void Traverse(string path, string displayPath, int currentDepth, int maxDepth, string? namePattern, char? typeFilter)
    {
        if (!VfsManager.TryStat(path, out VfsStat st)) return;

        string entryName = GetFileName(path);
        bool nameMatch = string.IsNullOrEmpty(namePattern) || MatchPattern(entryName, namePattern);
        bool typeMatch = !typeFilter.HasValue ||
            (typeFilter.Value == 'd' && st.IsDirectory) ||
            (typeFilter.Value == 'f' && !st.IsDirectory);

        if (nameMatch && typeMatch)
        {
            Output.WriteLine(displayPath, st.IsDirectory ? ConsoleColor.Cyan : ConsoleColor.White);
        }

        if (!st.IsDirectory || currentDepth >= maxDepth) return;

        if (!VfsManager.TryOpenDirectory(path, out var dir) || dir == null) return;

        IReadOnlyList<IVfsInode>? entries = null;
        using (dir)
        {
            dir.TryReadDir(out entries);
        }

        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.Name) || e.Name == "." || e.Name == "..") continue;

            string childPath = path == "/" ? "/" + e.Name : path + "/" + e.Name;
            string childDisplay = displayPath == "/" ? "/" + e.Name :
                (displayPath.EndsWith('/') ? displayPath + e.Name : displayPath + "/" + e.Name);

            Traverse(childPath, childDisplay, currentDepth + 1, maxDepth, namePattern, typeFilter);
        }
    }

    private static bool MatchPattern(string name, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.StartsWith('*') && pattern.EndsWith('*') && pattern.Length > 2)
            return name.Contains(pattern[1..^1], StringComparison.OrdinalIgnoreCase);
        if (pattern.StartsWith('*'))
            return name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        if (pattern.EndsWith('*'))
            return name.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFileName(string path)
    {
        int slash = path.LastIndexOf('/');
        return (slash >= 0 && slash < path.Length - 1) ? path[(slash + 1)..] : path;
    }

    public static void Help()
    {
        Output.WriteLine("Usage: find [PATH] [OPTIONS]", ConsoleColor.White);
        Output.WriteLine("Search for files in a directory hierarchy.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -name PATTERN     base of file name matches PATTERN (wildcards: *, *.ext)", ConsoleColor.Gray);
        Output.WriteLine("  -type f|d         file is of type: f (regular file), d (directory)", ConsoleColor.Gray);
        Output.WriteLine("  -maxdepth N       descend at most N levels of directories", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help        display this help and exit", ConsoleColor.Gray);
    }
}
