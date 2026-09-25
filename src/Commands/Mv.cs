// Mv.cs — mv command: move (rename) files and directories
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Mv
{
    public static void Run(int pid, string[] args)
    {
        bool force = false, verbose = false;
        var sources = new List<string>();
        string target = string.Empty;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--"))
            {
                if (a == "--force") force = true;
                else if (a == "--verbose") verbose = true;
                else
                {
                    Output.WriteLine($"mv: unrecognized option '{a}'", ConsoleColor.Red);
                    Output.WriteLine("Try 'mv --help' for more information.", ConsoleColor.Gray);
                    PManager.Exit(pid, 1);
                    return;
                }
            }
            else if (a.StartsWith('-') && a.Length > 1)
            {
                for (int j = 1; j < a.Length; j++)
                {
                    char c = a[j];
                    if (c == 'f') force = true;
                    else if (c == 'v') verbose = true;
                    else
                    {
                        Output.WriteLine($"mv: invalid option -- '{c}'", ConsoleColor.Red);
                        Output.WriteLine("Try 'mv --help' for more information.", ConsoleColor.Gray);
                        PManager.Exit(pid, 1);
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
            Output.WriteLine("usage: mv [OPTION]... SOURCE... DEST", ConsoleColor.Yellow);
            Output.WriteLine("Try 'mv --help' for more information.", ConsoleColor.Gray);
            PManager.Exit(pid, 1);
            return;
        }

        target = sources[^1];
        sources.RemoveAt(sources.Count - 1);

        string targetPath = CManager.ResolvePath(target);
        bool targetIsDir = VfsManager.TryStat(targetPath, out VfsStat targetStat) && targetStat.IsDirectory;

        if (sources.Count > 1 && !targetIsDir)
        {
            Output.WriteLine($"mv: target '{target}' is not a directory", ConsoleColor.Red);
            PManager.Exit(pid, 1);
            return;
        }

        bool hasError = false;
        foreach (string src in sources)
        {
            string srcPath = CManager.ResolvePath(src);
            if (!VfsManager.TryStat(srcPath, out VfsStat srcStat))
            {
                Output.WriteLine($"mv: cannot stat '{src}': No such file or directory", ConsoleColor.Red);
                hasError = true;
                continue;
            }

            string destPath = targetPath;
            if (targetIsDir)
            {
                string name = GetFileName(srcPath);
                destPath = targetPath == "/" ? "/" + name : targetPath + "/" + name;
            }

            if (!MoveItem(srcPath, destPath, srcStat, force, verbose)) hasError = true;
        }

        if (hasError) PManager.Exit(pid, 1);
    }

    private static bool MoveItem(string srcPath, string destPath, VfsStat srcStat, bool force, bool verbose)
    {
        if (srcPath == destPath) return true;

        if (srcStat.IsDirectory)
        {
            if (global::System.IO.Directory.Exists(srcPath))
            {
                try
                {
                    global::System.IO.Directory.Move(srcPath, destPath);
                    if (verbose) Output.WriteLine($"renamed '{srcPath}' -> '{destPath}'", ConsoleColor.Gray);
                    return true;
                }
                catch { }
            }
        }
        else
        {
            if (global::System.IO.File.Exists(srcPath))
            {
                try
                {
                    global::System.IO.File.Move(srcPath, destPath);
                    if (verbose) Output.WriteLine($"renamed '{srcPath}' -> '{destPath}'", ConsoleColor.Gray);
                    return true;
                }
                catch { }
            }
        }

        if (srcStat.IsDirectory)
        {
            if (Cp.CopyDir(srcPath, destPath, force, verbose))
            {
                RemoveDirRecursive(srcPath);
                return true;
            }
        }
        else
        {
            if (Cp.CopyFile(srcPath, destPath, srcStat, force, verbose))
            {
                VfsManager.TryUnlink(srcPath);
                return true;
            }
        }

        Output.WriteLine($"mv: cannot move '{srcPath}' to '{destPath}': Operation failed", ConsoleColor.Red);
        return false;
    }

    private static void RemoveDirRecursive(string path)
    {
        if (!VfsManager.TryOpenDirectory(path, out var dir) || dir == null) return;

        IReadOnlyList<IVfsInode>? entries = null;
        using (dir)
        {
            dir.TryReadDir(out entries);
        }

        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.Name) || e.Name == "." || e.Name == "..") continue;

                string child = path == "/" ? "/" + e.Name : path + "/" + e.Name;
                if (VfsManager.TryStat(child, out VfsStat st))
                {
                    if (st.IsDirectory) RemoveDirRecursive(child);
                    else VfsManager.TryUnlink(child);
                }
            }
        }

        VfsManager.TryRemoveDirectory(path);
    }

    private static string GetFileName(string path)
    {
        int slash = path.LastIndexOf('/');
        return (slash >= 0 && slash < path.Length - 1) ? path[(slash + 1)..] : path;
    }

    public static void Help()
    {
        Output.WriteLine("Usage: mv [OPTION]... SOURCE... DEST", ConsoleColor.White);
        Output.WriteLine("Rename SOURCE to DEST, or move SOURCE(s) to DIRECTORY.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -f, --force     do not prompt before overwriting", ConsoleColor.Gray);
        Output.WriteLine("  -v, --verbose   explain what is being done", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help      display this help and exit", ConsoleColor.Gray);
    }
}
