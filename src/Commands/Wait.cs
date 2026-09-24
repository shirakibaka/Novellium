// Wait.cs — wait command: wait for background process completion
using System;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Wait
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length < 2)
        {
            Output.WriteLine("usage: wait <pid>", ConsoleColor.Yellow);
            Output.WriteLine("Try 'wait --help' for more information.", ConsoleColor.Gray);
            return;
        }

        if (args[1] is "-h" or "--help") { Help(); return; }

        if (!int.TryParse(args[1], out int targetPid))
        {
            Output.WriteLine("wait: invalid pid", ConsoleColor.Red);
            return;
        }

        if (targetPid == pid)
        {
            Output.WriteLine("wait: cannot wait for itself", ConsoleColor.Red);
            return;
        }

        PInfo? caller = PManager.Get(pid);
        if (caller == null)
        {
            Output.WriteLine("wait: caller process not found", ConsoleColor.Red);
            return;
        }

        int parentPid = caller.Value.ParentPid;
        JManager.SetWaited(targetPid, true);

        try
        {
            if (!PManager.Wait(parentPid, targetPid, out int exitCode, pid))
            {
                if (PManager.IsKillReq(pid)) return;
                Output.WriteLine($"wait: process {targetPid} not found", ConsoleColor.Red);
                return;
            }

            JManager.Remove(targetPid, out _);
            Output.WriteLine($"wait: process {targetPid} exited with {exitCode}");
        }
        finally
        {
            JManager.SetWaited(targetPid, false);
        }
    }

    public static void Help()
    {
        Output.WriteLine("Usage: wait <pid>", ConsoleColor.White);
        Output.WriteLine("Wait for a child process to terminate and collect its exit status.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Arguments:", ConsoleColor.White);
        Output.WriteLine("  <pid>         child process identifier to wait for", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}