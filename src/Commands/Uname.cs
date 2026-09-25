// Uname.cs — uname command: print system and kernel information
using System;
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Uname
{
    public static void Run(int pid, string[] args)
    {
        bool all = false, sys = false, node = false, rel = false, ver = false, mach = false;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-a" or "--all") all = true;
            else if (a.StartsWith('-') && a.Length > 1)
            {
                for (int j = 1; j < a.Length; j++)
                {
                    char c = a[j];
                    if (c == 'a') all = true;
                    else if (c == 's') sys = true;
                    else if (c == 'n') node = true;
                    else if (c == 'r') rel = true;
                    else if (c == 'v') ver = true;
                    else if (c == 'm') mach = true;
                    else
                    {
                        Output.WriteLine($"uname: invalid option -- '{c}'", ConsoleColor.Red);
                        Output.WriteLine("Try 'uname --help' for more information.", ConsoleColor.Gray);
                        PManager.Exit(pid, 1);
                        return;
                    }
                }
            }
        }

        if (all || (!sys && !node && !rel && !ver && !mach))
        {
            Output.WriteLine(all
                ? "Novellium novellium 0.1.0 #1 SMP Cosmos x86_64 Limine/Gen3 GNU/Novellium"
                : "Novellium");
            return;
        }

        var parts = new List<string>(5);
        if (sys) parts.Add("Novellium");
        if (node) parts.Add("novellium");
        if (rel) parts.Add("0.1.0");
        if (ver) parts.Add("#1 SMP Cosmos Limine/Gen3");
        if (mach) parts.Add("x86_64");

        Output.WriteLine(string.Join(' ', parts));
    }

    public static void Help()
    {
        Output.WriteLine("Usage: uname [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Print certain system information. With no OPTION, same as -s.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -a, --all                print all information", ConsoleColor.Gray);
        Output.WriteLine("  -s, --kernel-name        print the kernel name", ConsoleColor.Gray);
        Output.WriteLine("  -n, --nodename           print the network node hostname", ConsoleColor.Gray);
        Output.WriteLine("  -r, --kernel-release     print the kernel release", ConsoleColor.Gray);
        Output.WriteLine("  -v, --kernel-version     print the kernel version", ConsoleColor.Gray);
        Output.WriteLine("  -m, --machine            print the machine hardware name", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help               display this help and exit", ConsoleColor.Gray);
    }
}
