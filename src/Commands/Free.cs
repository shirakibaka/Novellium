// Free.cs — free command: display amount of free and used system memory
using System;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Free
{
    public static void Run(int pid, string[] args)
    {
        bool human = false;
        long unit = 1024;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--human") human = true;
            else if (a is "-m" or "--mega") unit = 1024 * 1024;
            else if (a is "-k" or "--kilo") unit = 1024;
            else if (a is "-b" or "--bytes") unit = 1;
            else
            {
                Output.WriteLine($"free: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'free --help' for more information.", ConsoleColor.Gray);
                return;
            }
        }

        long total = 256L * 1024 * 1024;
        long kBase = 18L * 1024 * 1024;
        long pUsage = (long)PManager.Count * 2L * 1024 * 1024;
        long used = Math.Min(kBase + pUsage, total);
        long free = total - used;

        Output.WriteLine($"               {"total",-12}{"used",-12}{"free",-12}", ConsoleColor.White);

        string totStr = human ? FmtBytes(total) : (total / unit).ToString();
        string useStr = human ? FmtBytes(used) : (used / unit).ToString();
        string freStr = human ? FmtBytes(free) : (free / unit).ToString();

        Output.WriteLine($"Mem:           {totStr,-12}{useStr,-12}{freStr,-12}", ConsoleColor.Gray);
    }

    private static string FmtBytes(long b)
    {
        if (b >= 1024L * 1024 * 1024) return $"{b / (1024L * 1024 * 1024)}Gi";
        if (b >= 1024L * 1024) return $"{b / (1024L * 1024)}Mi";
        if (b >= 1024L) return $"{b / 1024L}Ki";
        return $"{b}B";
    }

    public static void Help()
    {
        Output.WriteLine("Usage: free [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Display amount of free and used memory in the system.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -b, --bytes  show output in bytes", ConsoleColor.Gray);
        Output.WriteLine("  -k, --kilo   show output in kibibytes (default)", ConsoleColor.Gray);
        Output.WriteLine("  -m, --mega   show output in mebibytes", ConsoleColor.Gray);
        Output.WriteLine("  -h, --human  show human-readable output", ConsoleColor.Gray);
        Output.WriteLine("      --help   display this help and exit", ConsoleColor.Gray);
    }
}
