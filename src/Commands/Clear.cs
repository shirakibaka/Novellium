// Clear.cs — clear command: clear terminal screen
using System;
using Novellium.IO;

namespace Novellium.Commands;

public static class Clear
{
    public static void Run(int pid, string[] args)
    {
        try { Output.Clear(); }
        catch
        {
            for (int i = 0; i < 30; i++) Output.WriteLine();
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: clear", ConsoleColor.White);
        Output.WriteLine("Clear the terminal screen.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
