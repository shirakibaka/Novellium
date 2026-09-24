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
            if (topic is "-h" or "--help") { Show(); return; }

            Action? action = topic switch
            {
                "ls" => Ls.Help,
                "cat" => Cat.Help,
                "cd" => Cd.Help,
                "pwd" => Pwd.Help,
                "touch" => Touch.Help,
                "mkdir" => Mkdir.Help,
                "rm" => Rm.Help,
                "rmdir" => Rmdir.Help,
                "df" => Df.Help,
                "stat" => Stat.Help,
                "uname" => Uname.Help,
                "uptime" => Uptime.Help,
                "free" => Free.Help,
                "ps" => Ps.Help,
                "jobs" => Jobs.Help,
                "kill" => Kill.Help,
                "wait" => Wait.Help,
                "sleep" => Sleep.Help,
                "dmesg" => Dmesg.Help,
                "clear" => Clear.Help,
                "test" => Test.Help,
                "help" => Show,
                _ => null
            };

            if (action != null) { action(); return; }
            Output.WriteLine($"help: no help topics match '{args[1]}'. Try 'help'.", ConsoleColor.Red);
            return;
        }

        Show();
    }

    private static void Show()
    {
        Output.WriteLine("Novellium Builtin Commands:", ConsoleColor.White);
        Output.WriteLine();
        Output.WriteLine("  help [command]       display information about builtin commands", ConsoleColor.Gray);
        Output.WriteLine("  ls [options] [path]  list directory contents", ConsoleColor.Gray);
        Output.WriteLine("  cat [options] [file] concatenate and display files", ConsoleColor.Gray);
        Output.WriteLine("  cd [directory]       change the current working directory", ConsoleColor.Gray);
        Output.WriteLine("  pwd                  print the current working directory", ConsoleColor.Gray);
        Output.WriteLine("  touch [file]...      create empty file or update timestamp", ConsoleColor.Gray);
        Output.WriteLine("  mkdir [-p] [dir]...  create directory", ConsoleColor.Gray);
        Output.WriteLine("  rm [-f] [file]...    remove (unlink) file", ConsoleColor.Gray);
        Output.WriteLine("  rmdir [dir]...       remove empty directory", ConsoleColor.Gray);
        Output.WriteLine("  df [-h]              show filesystem disk space usage", ConsoleColor.Gray);
        Output.WriteLine("  stat [file]...       display file or filesystem status", ConsoleColor.Gray);
        Output.WriteLine("  uname [-a]           print system and kernel information", ConsoleColor.Gray);
        Output.WriteLine("  uptime               show system uptime and process count", ConsoleColor.Gray);
        Output.WriteLine("  free [-h|-m|-k]      display memory and heap usage", ConsoleColor.Gray);
        Output.WriteLine("  ps                   report snapshot of current processes", ConsoleColor.Gray);
        Output.WriteLine("  jobs                 list active background jobs", ConsoleColor.Gray);
        Output.WriteLine("  kill <pid>           terminate a process by PID", ConsoleColor.Gray);
        Output.WriteLine("  wait <pid>           wait for a child process to terminate", ConsoleColor.Gray);
        Output.WriteLine("  sleep <seconds>      delay for a specified number of seconds", ConsoleColor.Gray);
        Output.WriteLine("  dmesg [options]      print system and kernel log buffer", ConsoleColor.Gray);
        Output.WriteLine("  clear                clear the terminal screen", ConsoleColor.Gray);
        Output.WriteLine("  test [suite]         run kernel and command automated test suites", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Append '&' to run any command in background.", ConsoleColor.DarkGray);
        Output.WriteLine("Use 'help <command>' or '<command> --help' for details.", ConsoleColor.DarkGray);
    }
}
