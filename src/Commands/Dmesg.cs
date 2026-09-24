// Dmesg.cs — dmesg command: inspect and control the syslog ring buffer
using System;
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Services;

namespace Novellium.Commands;

public static class Dmesg
{
    public static void Run(int pid, string[] args)
    {
        bool clear = false;
        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-c" or "--clear") clear = true;
            else
            {
                Output.WriteLine($"dmesg: invalid option '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'dmesg --help' for more information.", ConsoleColor.Gray);
                return;
            }
        }

        IReadOnlyList<LogEntry> logs = Syslogd.GetRecentLogs();
        if (logs.Count == 0)
        {
            Output.WriteLine("dmesg: log buffer is empty", ConsoleColor.DarkGray);
        }
        else
        {
            foreach (var log in logs)
            {
                string time = $"[{log.Timestamp.Year:D4}-{log.Timestamp.Month:D2}-{log.Timestamp.Day:D2} {log.Timestamp.Hour:D2}:{log.Timestamp.Minute:D2}:{log.Timestamp.Second:D2}]";
                Output.Write(time + " ", ConsoleColor.DarkGray);

                var (col, tag) = log.Level switch
                {
                    LogLevel.Ok => (ConsoleColor.Green, "OK"),
                    LogLevel.Info => (ConsoleColor.Cyan, "INFO"),
                    LogLevel.Warning => (ConsoleColor.Yellow, "WARN"),
                    LogLevel.Error => (ConsoleColor.Red, "ERROR"),
                    LogLevel.Debug => (ConsoleColor.Magenta, "DEBUG"),
                    _ => (ConsoleColor.White, "LOG")
                };

                Output.Write($"[{tag,-5}] ", col);
                Output.Write($"[{log.Facility}] ", ConsoleColor.Gray);
                Output.WriteLine(log.Message, ConsoleColor.White);
            }
        }

        if (clear)
        {
            Syslogd.ClearRingBuffer();
            Output.WriteLine("dmesg: log buffer cleared", ConsoleColor.Yellow);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: dmesg [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Print or control the kernel and system log buffer.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -c, --clear   clear the log buffer after printing", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
