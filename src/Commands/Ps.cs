// Ps.cs — ps command: report a snapshot of active processes
using System;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Ps
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length > 1 && args[1] is "-h" or "--help") { Help(); return; }
        PManager.List();
    }

    public static void Help()
    {
        Output.WriteLine("Usage: ps [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Report a snapshot of the current processes.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}