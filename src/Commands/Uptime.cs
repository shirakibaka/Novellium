// Uptime.cs — uptime command: show how long the system has been running
using System;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Uptime
{
    public static void Run(int pid, string[] args)
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        string time = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}";
        string upStr = up.TotalDays >= 1
            ? $"{(int)up.TotalDays} day(s), {up.Hours:D2}:{up.Minutes:D2}"
            : (up.TotalHours >= 1 ? $"{up.Hours:D2}:{up.Minutes:D2}" : $"{up.Minutes} min");

        Output.WriteLine($" {time}  up {upStr},  {PManager.Count} processes");
    }

    public static void Help()
    {
        Output.WriteLine("Usage: uptime [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Tell how long the system has been running.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
