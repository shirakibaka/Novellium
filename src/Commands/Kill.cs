// Kill.cs — kill command: terminate process via signal
using System;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Kill
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length < 2)
        {
            Output.WriteLine("usage: kill <pid>", ConsoleColor.Yellow);
            Output.WriteLine("Try 'kill --help' for more information.", ConsoleColor.Gray);
            return;
        }


        if (!int.TryParse(args[1], out int targetPid))
        {
            Output.WriteLine("kill: invalid pid", ConsoleColor.Red);
            return;
        }

        if (!PManager.Kill(targetPid))
            Output.WriteLine($"kill: process {targetPid} not found or already stopped", ConsoleColor.Red);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: kill [OPTION] <pid>", ConsoleColor.White);
        Output.WriteLine("Send termination signal to a process by its PID.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Arguments:", ConsoleColor.White);
        Output.WriteLine("  <pid>         process identifier to terminate", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}