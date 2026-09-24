// Cd.cs — cd command: change current working directory
using System;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Cd
{
    public static void Run(int pid, string[] args)
    {
        string target = args.Length > 1 ? CManager.ResolvePath(args[1]) : "/";
        if (!VfsManager.TryStat(target, out VfsStat stat))
        {
            Output.WriteLine($"cd: no such file or directory: {args[1]}", ConsoleColor.Red);
            return;
        }
        if (!stat.IsDirectory)
        {
            Output.WriteLine($"cd: not a directory: {args[1]}", ConsoleColor.Red);
            return;
        }

        int activePid = PManager.CurrentPid;
        PManager.SetCwd(activePid, target);
        if (pid > 0) PManager.SetCwd(pid, target);
        int parentPid = PManager.GetParentPid(pid);
        if (parentPid > 0) PManager.SetCwd(parentPid, target);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: cd [DIRECTORY]", ConsoleColor.White);
        Output.WriteLine("Change the shell working directory to DIRECTORY (default is /).", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
