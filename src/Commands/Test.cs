// Test.cs — test command: execute internal system and command test suites
using System;
using Novellium.IO;
using Novellium.Tests;

namespace Novellium.Commands;

public static class Test
{
    public static void Run(int pid, string[] args)
    {
        MainTest.Run();
    }

    public static void Help()
    {
        Output.WriteLine("Usage: test", ConsoleColor.White);
        Output.WriteLine("Run Novellium unified master integration test suite.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
