// Echo.cs — echo command: print text to output
using System;
using System.Text;
using Novellium.IO;

namespace Novellium.Commands;

public static class Echo
{
    public static void Run(int pid, string[] args)
    {
        bool newline = true;
        int startIdx = 1;

        if (args.Length > 1 && args[1] == "-n")
        {
            newline = false;
            startIdx = 2;
        }

        StringBuilder sb = new();
        for (int i = startIdx; i < args.Length; i++)
        {
            if (i > startIdx) sb.Append(' ');
            sb.Append(args[i]);
        }

        if (newline) Output.WriteLine(sb.ToString());
        else Output.Write(sb.ToString());
    }

    public static void Help()
    {
        Output.WriteLine("Usage: echo [OPTION]... [STRING]...", ConsoleColor.White);
        Output.WriteLine("Print STRING(s) to standard output.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -n           do not output the trailing newline", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help   display this help and exit", ConsoleColor.Gray);
    }
}
