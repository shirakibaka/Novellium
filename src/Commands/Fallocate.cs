// Fallocate.cs — fallocate command: preallocate space to a file
using System;
using System.IO;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.IO.Cache;
using Novellium.Process;

namespace Novellium.Commands;

public static class Fallocate
{
    public static void Run(int pid, string[] args)
    {
        long length = -1;
        string? targetFile = null;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if ((a is "-l" or "--length") && i + 1 < args.Length)
            {
                length = ParseSize(args[i + 1]);
                i++;
            }
            else if (a is "-h" or "--help")
            {
                Help();
                return;
            }
            else if (!a.StartsWith('-') && targetFile == null)
            {
                targetFile = a;
            }
        }

        if (length <= 0 || string.IsNullOrEmpty(targetFile))
        {
            Output.WriteLine("fallocate: length and file operand required", ConsoleColor.Red);
            Output.WriteLine("Try 'fallocate --help' for more information.", ConsoleColor.Gray);
            PManager.Exit(pid, 1);
            return;
        }

        string fullPath = CManager.ResolvePath(targetFile);

        if (VfsManager.TryStat(fullPath, out VfsStat st) && st.IsDirectory)
        {
            Output.WriteLine($"fallocate: '{targetFile}': Is a directory", ConsoleColor.Red);
            PManager.Exit(pid, 1);
            return;
        }

        if (VfsManager.TryStat(fullPath, out _))
        {
            VfsManager.TryUnlink(fullPath);
        }

        if (!VfsManager.TryCreateFile(fullPath, (VfsMode)420))
        {
            Output.WriteLine($"fallocate: '{targetFile}': Cannot create file", ConsoleColor.Red);
            PManager.Exit(pid, 1);
            return;
        }

        if (!VfsManager.TryOpenFile(fullPath, out var handle) || handle == null)
        {
            Output.WriteLine($"fallocate: '{targetFile}': Cannot open file for writing", ConsoleColor.Red);
            PManager.Exit(pid, 1);
            return;
        }

        using (handle)
        {
            byte[] zeroBuffer = new byte[(int)length];
            handle.Write(zeroBuffer);
            handle.TryFlush();
        }

        // Invalidate VFS cache slot for this modified file
        VfsCacheEngine.Invalidate(fullPath);

        Output.Write("Preallocated ", ConsoleColor.Gray);
        Output.Write($"{FmtBytes(length)}", ConsoleColor.Green);
        Output.WriteLine($" for '{targetFile}'", ConsoleColor.Gray);
    }

    private static long ParseSize(string s)
    {
        s = s.Trim().ToUpperInvariant();
        long multiplier = 1;

        if (s.EndsWith("GI")) { multiplier = 1024L * 1024 * 1024; s = s[..^2]; }
        else if (s.EndsWith("G")) { multiplier = 1024L * 1024 * 1024; s = s[..^1]; }
        else if (s.EndsWith("MI")) { multiplier = 1024L * 1024; s = s[..^2]; }
        else if (s.EndsWith("M")) { multiplier = 1024L * 1024; s = s[..^1]; }
        else if (s.EndsWith("KI")) { multiplier = 1024L; s = s[..^2]; }
        else if (s.EndsWith("K")) { multiplier = 1024L; s = s[..^1]; }
        else if (s.EndsWith("B")) { multiplier = 1L; s = s[..^1]; }

        if (long.TryParse(s, out long val)) return val * multiplier;
        return -1;
    }

    private static string FmtBytes(long b)
    {
        if (b >= 1024L * 1024 * 1024) return $"{b / (1024L * 1024 * 1024)} GiB";
        if (b >= 1024L * 1024) return $"{b / (1024L * 1024)} MiB";
        if (b >= 1024L) return $"{b / 1024L} KiB";
        return $"{b} Bytes";
    }

    public static void Help()
    {
        Output.WriteLine("Usage: fallocate -l LENGTH FILE", ConsoleColor.White);
        Output.WriteLine("Preallocate space to a file.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -l, --length <N>  length of file (e.g. 512, 4K, 1M)", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help        display this help and exit", ConsoleColor.Gray);
    }
}
