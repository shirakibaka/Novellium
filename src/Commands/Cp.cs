// Cp.cs — cp command: copy files and directories
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Cp
{
    public static void Run(int pid, string[] args)
    {
        bool recursive = false, force = false, verbose = false;
        var sources = new List<string>();
        string target = string.Empty;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--"))
            {
                if (a == "--recursive") recursive = true;
                else if (a == "--force") force = true;
                else if (a == "--verbose") verbose = true;
                else
                {
                    Output.WriteLine($"cp: unrecognized option '{a}'", ConsoleColor.Red);
                    Output.WriteLine("Try 'cp --help' for more information.", ConsoleColor.Gray);
                    return;
                }
            }
            else if (a.StartsWith('-') && a.Length > 1)
            {
                for (int j = 1; j < a.Length; j++)
                {
                    char c = a[j];
                    if (c is 'r' or 'R') recursive = true;
                    else if (c == 'f') force = true;
                    else if (c == 'v') verbose = true;
                    else
                    {
                        Output.WriteLine($"cp: invalid option -- '{c}'", ConsoleColor.Red);
                        Output.WriteLine("Try 'cp --help' for more information.", ConsoleColor.Gray);
                        return;
                    }
                }
            }
            else
            {
                sources.Add(a);
            }
        }

        if (sources.Count < 2)
        {
            Output.WriteLine("usage: cp [OPTION]... SOURCE... DEST", ConsoleColor.Yellow);
            Output.WriteLine("Try 'cp --help' for more information.", ConsoleColor.Gray);
            return;
        }

        target = sources[^1];
        sources.RemoveAt(sources.Count - 1);

        string targetPath = CManager.ResolvePath(target);
        bool targetIsDir = VfsManager.TryStat(targetPath, out VfsStat targetStat) && targetStat.IsDirectory;

        if (sources.Count > 1 && !targetIsDir)
        {
            Output.WriteLine($"cp: target '{target}' is not a directory", ConsoleColor.Red);
            return;
        }

        foreach (string src in sources)
        {
            string srcPath = CManager.ResolvePath(src);
            if (!VfsManager.TryStat(srcPath, out VfsStat srcStat))
            {
                Output.WriteLine($"cp: cannot stat '{src}': No such file or directory", ConsoleColor.Red);
                continue;
            }

            string destPath = targetPath;
            if (targetIsDir)
            {
                string name = GetFileName(srcPath);
                destPath = targetPath == "/" ? "/" + name : targetPath + "/" + name;
            }

            if (srcStat.IsDirectory)
            {
                if (!recursive)
                {
                    Output.WriteLine($"cp: -r not specified; omitting directory '{src}'", ConsoleColor.Red);
                    continue;
                }
                CopyDir(srcPath, destPath, force, verbose);
            }
            else
            {
                CopyFile(srcPath, destPath, srcStat, force, verbose);
            }
        }
    }

    public static bool CopyFile(string srcPath, string destPath, VfsStat srcStat, bool force, bool verbose)
    {
        if (srcPath == destPath) return true;

        if (VfsManager.TryStat(destPath, out _))
        {
            if (force) VfsManager.TryUnlink(destPath);
            else VfsManager.TryUnlink(destPath);
        }

        if (!VfsManager.TryOpenFile(srcPath, out var inHandle) || inHandle == null) return false;

        byte[] content;
        using (inHandle)
        {
            content = new byte[(int)srcStat.Size];
            if (content.Length > 0) inHandle.Read(content);
        }

        if (!VfsManager.TryCreateFile(destPath, srcStat.Mode)) return false;
        if (!VfsManager.TryOpenFile(destPath, out var outHandle) || outHandle == null) return false;

        using (outHandle)
        {
            if (content.Length > 0) outHandle.Write(content);
            outHandle.TryFlush();
        }

        if (verbose) Output.WriteLine($"'{srcPath}' -> '{destPath}'", ConsoleColor.Gray);
        return true;
    }

    public static bool CopyDir(string srcPath, string destPath, bool force, bool verbose)
    {
        if (!VfsManager.TryStat(destPath, out VfsStat destStat))
        {
            if (VfsManager.TryStat(srcPath, out VfsStat sStat))
            {
                VfsManager.TryCreateDirectory(destPath, sStat.Mode);
            }
            else
            {
                VfsManager.TryCreateDirectory(destPath, (VfsMode)493);
            }
        }

        if (verbose) Output.WriteLine($"'{srcPath}' -> '{destPath}'", ConsoleColor.Gray);

        if (!VfsManager.TryOpenDirectory(srcPath, out var dir) || dir == null) return false;

        IReadOnlyList<IVfsInode>? entries = null;
        using (dir)
        {
            dir.TryReadDir(out entries);
        }

        if (entries == null) return true;

        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.Name) || e.Name == "." || e.Name == "..") continue;

            string childSrc = srcPath == "/" ? "/" + e.Name : srcPath + "/" + e.Name;
            string childDest = destPath == "/" ? "/" + e.Name : destPath + "/" + e.Name;

            if (VfsManager.TryStat(childSrc, out VfsStat childStat))
            {
                if (childStat.IsDirectory)
                {
                    CopyDir(childSrc, childDest, force, verbose);
                }
                else
                {
                    CopyFile(childSrc, childDest, childStat, force, verbose);
                }
            }
        }
        return true;
    }

    private static string GetFileName(string path)
    {
        int slash = path.LastIndexOf('/');
        return (slash >= 0 && slash < path.Length - 1) ? path[(slash + 1)..] : path;
    }

    public static void Help()
    {
        Output.WriteLine("Usage: cp [OPTION]... SOURCE... DEST", ConsoleColor.White);
        Output.WriteLine("Copy SOURCE to DEST, or multiple SOURCE(s) to DIRECTORY.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -r, -R, --recursive   copy directories recursively", ConsoleColor.Gray);
        Output.WriteLine("  -f, --force           force overwrite existing destination files", ConsoleColor.Gray);
        Output.WriteLine("  -v, --verbose         explain what is being done", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help            display this help and exit", ConsoleColor.Gray);
    }
}
