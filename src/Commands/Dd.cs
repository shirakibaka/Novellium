// Dd.cs — dd command: convert and copy a file with block cache integration
using System;
using System.IO;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.IO.Cache;
using Novellium.Process;

namespace Novellium.Commands;

public static class Dd
{
    private static readonly Random Rnd = new(42);

    public static void Run(int pid, string[] args)
    {
        string? inFile = null;
        string? outFile = null;
        int blockSize = 512;
        int count = -1;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("if=")) inFile = a[3..];
            else if (a.StartsWith("of=")) outFile = a[3..];
            else if (a.StartsWith("bs=")) blockSize = (int)ParseSize(a[3..]);
            else if (a.StartsWith("count=")) count = (int)ParseSize(a[6..]);
            else if (a is "-h" or "--help")
            {
                Help();
                return;
            }
        }

        if (string.IsNullOrEmpty(inFile) && string.IsNullOrEmpty(outFile))
        {
            Help();
            PManager.Exit(pid, 1);
            return;
        }

        blockSize = Math.Max(1, blockSize);
        int maxBlocks = (count > 0) ? count : 1;

        byte[] totalInputBuffer;

        // Input Processing
        if (inFile is "/dev/urandom" or "urandom")
        {
            totalInputBuffer = new byte[blockSize * maxBlocks];
            Rnd.NextBytes(totalInputBuffer);
        }
        else if (inFile is "/dev/zero" or "zero")
        {
            totalInputBuffer = new byte[blockSize * maxBlocks];
        }
        else if (!string.IsNullOrEmpty(inFile))
        {
            string inPath = CManager.ResolvePath(inFile);
            if (!VfsCacheEngine.TryReadFile(inPath, out totalInputBuffer!) || totalInputBuffer == null)
            {
                if (!VfsManager.TryStat(inPath, out VfsStat st) || st.IsDirectory)
                {
                    Output.WriteLine($"dd: failed to open '{inFile}': No such file or directory", ConsoleColor.Red);
                    PManager.Exit(pid, 1);
                    return;
                }

                if (!VfsManager.TryOpenFile(inPath, out var inHandle) || inHandle == null)
                {
                    Output.WriteLine($"dd: failed to open '{inFile}' for reading", ConsoleColor.Red);
                    PManager.Exit(pid, 1);
                    return;
                }

                using (inHandle)
                {
                    int totalBytesToRead = (count > 0) ? Math.Min((int)st.Size, blockSize * maxBlocks) : (int)st.Size;
                    totalInputBuffer = new byte[totalBytesToRead];
                    inHandle.Read(totalInputBuffer);
                }

                // Cache the file for subsequent reads
                ulong key = VfsCacheEngine.ComputeKey(inPath, 0);
                VfsCacheEngine.Engine.Put(key, totalInputBuffer);
            }
        }
        else
        {
            // Reading from Stdin
            string stdinText = Output.GetStdin();
            totalInputBuffer = global::System.Text.Encoding.UTF8.GetBytes(stdinText);
        }

        int recordsIn = (totalInputBuffer.Length + blockSize - 1) / blockSize;
        if (count > 0 && recordsIn > count) recordsIn = count;
        int bytesToWrite = Math.Min(totalInputBuffer.Length, recordsIn * blockSize);

        byte[] outputBuffer = new byte[bytesToWrite];
        Array.Copy(totalInputBuffer, 0, outputBuffer, 0, bytesToWrite);

        // Output Processing
        if (!string.IsNullOrEmpty(outFile))
        {
            string outPath = CManager.ResolvePath(outFile);
            if (VfsManager.TryStat(outPath, out _)) VfsManager.TryUnlink(outPath);
            if (!VfsManager.TryCreateFile(outPath, (VfsMode)420))
            {
                Output.WriteLine($"dd: failed to open '{outFile}': Cannot create file", ConsoleColor.Red);
                PManager.Exit(pid, 1);
                return;
            }

            if (!VfsManager.TryOpenFile(outPath, out var outHandle) || outHandle == null)
            {
                Output.WriteLine($"dd: failed to open '{outFile}' for writing", ConsoleColor.Red);
                PManager.Exit(pid, 1);
                return;
            }

            using (outHandle)
            {
                outHandle.Write(outputBuffer);
                outHandle.TryFlush();
            }

            // Invalidate cache for modified output file
            VfsCacheEngine.Invalidate(outPath);
        }
        else
        {
            Output.Write(global::System.Text.Encoding.UTF8.GetString(outputBuffer));
            Output.WriteLine();
        }

        // Print dd summary stats
        Output.WriteLine($"{recordsIn}+0 records in", ConsoleColor.Gray);
        Output.WriteLine($"{recordsIn}+0 records out", ConsoleColor.Gray);
        Output.Write($"{bytesToWrite} bytes ", ConsoleColor.Gray);
        Output.Write($"({FmtBytes(bytesToWrite)})", ConsoleColor.Green);
        Output.WriteLine(" copied", ConsoleColor.Gray);
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
        return 512;
    }

    private static string FmtBytes(long b)
    {
        if (b >= 1024L * 1024 * 1024) return $"{b / (1024L * 1024 * 1024)} GiB";
        if (b >= 1024L * 1024) return $"{b / (1024L * 1024)} MiB";
        if (b >= 1024L) return $"{b / 1024L} KiB";
        return $"{b} B";
    }

    public static void Help()
    {
        Output.WriteLine("Usage: dd [OPERAND]...", ConsoleColor.White);
        Output.WriteLine("Copy a file, converting and formatting according to the operands.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Operands:", ConsoleColor.White);
        Output.WriteLine("  if=FILE    read from FILE instead of standard input (e.g. /dev/urandom, /dev/zero)", ConsoleColor.Gray);
        Output.WriteLine("  of=FILE    write to FILE instead of standard output", ConsoleColor.Gray);
        Output.WriteLine("  bs=BYTES   read and write up to BYTES bytes at a time (default: 512)", ConsoleColor.Gray);
        Output.WriteLine("  count=N    copy only N input blocks", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help display this help and exit", ConsoleColor.Gray);
    }
}
