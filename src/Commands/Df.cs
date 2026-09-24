// Df.cs — df command: report file system disk space usage
using System;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Commands;

public static class Df
{
    public static void Run(int pid, string[] args)
    {
        bool human = false;
        long unit = 1024;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--human-readable") human = true;
            else if (a is "-k") unit = 1024;
            else if (a is "-m") unit = 1024 * 1024;
            else if (a.StartsWith('-'))
            {
                Output.WriteLine($"df: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'df --help' for more information.", ConsoleColor.Gray);
                return;
            }
        }

        if (!VfsManager.TryStatFs("/", out VfsStatFs st))
        {
            Output.WriteLine("df: failed to get filesystem statistics", ConsoleColor.Red);
            return;
        }

        ulong bs = st.BlockSize > 0 ? st.BlockSize : 1024;
        ulong total = st.Blocks * bs;
        ulong free = st.Bfree * bs;
        ulong used = total >= free ? total - free : 0;
        ulong pct = total > 0 ? (used * 100 / total) : 0;

        Output.WriteLine($"{"Filesystem",-14} {"Size",-10} {"Used",-10} {"Avail",-10} {"Use%",-6} {"Mounted on"}", ConsoleColor.White);

        string sz = human ? FmtBytes(total) : ((long)(total / (ulong)unit)).ToString();
        string us = human ? FmtBytes(used) : ((long)(used / (ulong)unit)).ToString();
        string fr = human ? FmtBytes(free) : ((long)(free / (ulong)unit)).ToString();

        Output.WriteLine($"{"/dev/target",-14} {sz,-10} {us,-10} {fr,-10} {pct + "%",-6} {"/"}", ConsoleColor.Gray);
    }

    private static string FmtBytes(ulong b)
    {
        if (b >= 1024UL * 1024UL * 1024UL) return $"{b / (1024UL * 1024UL * 1024UL)}G";
        if (b >= 1024UL * 1024UL) return $"{b / (1024UL * 1024UL)}M";
        if (b >= 1024UL) return $"{b / 1024UL}K";
        return $"{b}B";
    }

    public static void Help()
    {
        Output.WriteLine("Usage: df [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Show information about the file system on which each FILE resides.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --human-readable  print sizes in powers of 1024 (e.g., 512M)", ConsoleColor.Gray);
        Output.WriteLine("  -k                    like --block-size=1K", ConsoleColor.Gray);
        Output.WriteLine("  -m                    like --block-size=1M", ConsoleColor.Gray);
        Output.WriteLine("      --help            display this help and exit", ConsoleColor.Gray);
    }
}
