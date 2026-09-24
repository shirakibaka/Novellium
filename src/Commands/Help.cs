// Help.cs — help command: built-in command documentation and usage guide
using System;
using Novellium.IO;

namespace Novellium.Commands;

public static class Help
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length > 1)
        {
            string topic = args[1].ToLower();
            CmdEntry? entry = CmdRegistry.Get(topic);
            if (entry != null)
            {
                entry.HelpHandler();
                return;
            }

            Output.WriteLine($"help: no help topics match '{args[1]}'. Try 'help'.", ConsoleColor.Red);
            return;
        }

        Show();
    }

    public static void Show()
    {
        Output.WriteLine("Novellium Commands:", ConsoleColor.White);
        Output.WriteLine();

        int maxLen = 0;
        foreach (CmdEntry cmd in CmdRegistry.GetAll())
        {
            if (cmd.Name.Length > maxLen) maxLen = cmd.Name.Length;
        }
        maxLen = Math.Max(maxLen + 2, 12);

        foreach (CmdEntry cmd in CmdRegistry.GetAll())
        {
            Output.WriteLine($"  {cmd.Name.PadRight(maxLen)} {cmd.Summary}", ConsoleColor.Gray);
        }

        Output.WriteLine();
        Output.WriteLine("Append '&' to run any command in background.", ConsoleColor.DarkGray);
        Output.WriteLine("Use 'help <command>' or '<command> --help' for details.", ConsoleColor.DarkGray);
    }
}
