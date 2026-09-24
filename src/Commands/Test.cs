// Test.cs — test command: execute internal system and command test suites
using System;
using Novellium.IO;
using Novellium.Tests;

namespace Novellium.Commands;

public static class Test
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length > 1 && args[1] is "-h" or "--help") { Help(); return; }

        string suite = args.Length > 1 ? args[1].ToLower() : "all";
        switch (suite)
        {
            case "commands" or "cmd":
                ComprehensiveCommandTests.Run();
                break;
            case "process" or "proc":
                ProcessTests.Run();
                break;
            default:
                CommandTests.Run();
                break;
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: test [SUITE]", ConsoleColor.White);
        Output.WriteLine("Run kernel automated test suites.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Suites:", ConsoleColor.White);
        Output.WriteLine("  all        run all command and comprehensive tests (default)", ConsoleColor.Gray);
        Output.WriteLine("  commands   run comprehensive filesystem & commands suite", ConsoleColor.Gray);
        Output.WriteLine("  process    run process manager test suite", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help display this help and exit", ConsoleColor.Gray);
    }
}
