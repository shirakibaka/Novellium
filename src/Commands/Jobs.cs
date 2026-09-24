// Jobs.cs — jobs command: list active background jobs
using System;
using Novellium.IO;

namespace Novellium.Commands;

public static class Jobs
{
    public static void Run(int pid, string[] args)
    {
        JManager.List();
    }

    public static void Help()
    {
        Output.WriteLine("Usage: jobs [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Display status of jobs in the current active session.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}