// Pwd.cs — pwd command: print name of current working directory
using System;
using Novellium.IO;

namespace Novellium.Commands;

public static class Pwd
{
    public static void Run(int pid, string[] args)
    {
        Output.WriteLine(CManager.CurrentDirectory);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: pwd", ConsoleColor.White);
        Output.WriteLine("Print the full filename of the current working directory.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
