// Sleep.cs — sleep command: delay for a specified amount of time
using System;
using System.Threading;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Sleep
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length < 2)
        {
            Output.WriteLine("usage: sleep <seconds>", ConsoleColor.Yellow);
            Output.WriteLine("Try 'sleep --help' for more information.", ConsoleColor.Gray);
            return;
        }


        if (!int.TryParse(args[1], out int sec) || sec < 0)
        {
            Output.WriteLine("sleep: invalid time", ConsoleColor.Red);
            return;
        }

        for (int i = 0; i < sec; i++)
        {
            if (PManager.IsKillReq(pid)) return;
            Thread.Sleep(1000);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: sleep <seconds>", ConsoleColor.White);
        Output.WriteLine("Pause execution for a specified number of seconds.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Arguments:", ConsoleColor.White);
        Output.WriteLine("  <seconds>     number of seconds to pause", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}