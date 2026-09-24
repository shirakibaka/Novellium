// STest.cs — System environment and VFS hierarchy test suite
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Tests;

public static class SystemTests
{
    private static int Passed;
    private static int Failed;
    private static readonly List<string> FailedTests = new();

    public static void Run()
    {
        Passed = 0;
        Failed = 0;
        FailedTests.Clear();

        Output.WriteLine("=== SYSTEM TESTS ===", ConsoleColor.Cyan);

        TestProcessManagerState();
        TestPidAllocation();
        TestRootFilesystemStructure();

        Output.WriteLine();

        if (Failed == 0)
        {
            Output.WriteLine($"RESULT: {Passed} passed", ConsoleColor.Green);
        }
        else
        {
            Output.WriteLine($"RESULT: {Passed} passed, {Failed} failed", ConsoleColor.Red);
            Output.WriteLine("Failed tests:", ConsoleColor.Red);
            foreach (string fail in FailedTests) Output.WriteLine($"  - {fail}", ConsoleColor.Red);
        }

        Output.WriteLine();
    }

    private static void TestProcessManagerState()
    {
        PInfo? k = PManager.Get(1);
        Check(k != null && k.Value.Pid == 1 && k.Value.ParentPid == 0 && k.Value.State == PState.Running, "kernel process state");
    }

    private static void TestPidAllocation()
    {
        int p1 = PManager.Start("sys-test-1", [], EmptyProcess);
        int p2 = PManager.Start("sys-test-2", [], EmptyProcess);

        Check(p2 == p1 + 1, "sequential PID allocation");

        PManager.Wait(1, p1, out _);
        PManager.Wait(1, p2, out _);
    }

    private static void TestRootFilesystemStructure()
    {
        if (!VfsManager.TryStat("/", out _))
        {
            OutputInfo.Warning("Root filesystem not mounted, skipping fs tests");
            return;
        }

        string[] requiredDirs = ["/bin", "/etc", "/home", "/home/user", "/var", "/var/log", "/tmp", "/root", "/dev", "/proc", "/usr"];
        bool allDirs = true;
        foreach (string d in requiredDirs)
        {
            if (!VfsManager.TryStat(d, out VfsStat st) || !st.IsDirectory)
            {
                allDirs = false;
                break;
            }
        }
        Check(allDirs, "root hierarchy directories exist");

        string[] requiredFiles = ["/etc/hostname", "/etc/os-release", "/etc/motd", "/etc/version"];
        bool allFiles = true;
        foreach (string f in requiredFiles)
        {
            if (!VfsManager.TryStat(f, out VfsStat st) || st.IsDirectory)
            {
                allFiles = false;
                break;
            }
        }
        Check(allFiles, "base configuration files exist");
    }

    private static void EmptyProcess(int pid, string[] args) { }

    private static void Check(bool cond, string name)
    {
        if (cond)
        {
            Passed++;
            OutputInfo.Test(true, name);
        }
        else
        {
            Failed++;
            FailedTests.Add(name);
            OutputInfo.Test(false, name);
        }
    }
}