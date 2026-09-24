// PTest.cs — Process manager and lifecycle test suite
using System;
using System.Collections.Generic;
using System.Threading;
using Novellium.Commands;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Tests;

public static class ProcessTests
{
    private static int Passed;
    private static int Failed;
    private static readonly List<string> FailedTests = new();

    public static void Run()
    {
        Passed = 0;
        Failed = 0;
        FailedTests.Clear();

        bool prevAutoReap = PManager.AutomaticOrphanReaping;
        PManager.AutomaticOrphanReaping = false;

        Output.WriteLine("=== PROCESS TESTS ===", ConsoleColor.Cyan);

        try
        {
            TestKernel();
            TestStart();
            TestExit();
            TestException();
            TestKill();
            TestWaitKill();
            TestWaitAndReap();
            TestZombieReap();
            TestReparenting();
            TestOrphanZombieReap();
            TestFullOrphanLifecycle();
            TestStartFailure();
            TestThreadTracking();
        }
        finally
        {
            PManager.AutomaticOrphanReaping = prevAutoReap;
        }

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

    private static void TestKernel()
    {
        PInfo? p = PManager.Get(1);
        Check(p != null, "kernel process exists");
        Check(p != null && p.Value.ParentPid == 0, "kernel has no parent");
        Check(p != null && p.Value.State == PState.Running, "kernel is running");
        Check(!PManager.Kill(1), "kernel cannot be killed");
    }

    private static void TestStart()
    {
        int pid = PManager.Start("test-start", [], (procPid, args) => Thread.Sleep(5));
        Check(pid > 1, "process receives valid PID");

        PInfo? p = PManager.Get(pid);
        Check(p != null, "started process exists");
        Check(p != null && p.Value.ParentPid == 1, "process has correct parent");

        bool waited = PManager.Wait(1, pid, out int exitCode);
        Check(waited, "parent waits for child");
        Check(exitCode == 0, "normal process exits with code 0");
        Check(PManager.Get(pid) == null, "wait reaps child");
    }

    private static void TestExit()
    {
        int pid = PManager.Start("test-exit", [], (procPid, args) => PManager.Exit(procPid, 42));
        bool waited = PManager.Wait(1, pid, out int exitCode);

        Check(waited, "parent waits for exited child");
        Check(exitCode == 42, "explicit exit code is preserved");
        Check(PManager.Get(pid) == null, "exited child is reaped");
    }

    private static void TestException()
    {
        int pid = PManager.Start("test-exception", [], (procPid, args) => throw new InvalidOperationException("simulated crash"));
        bool waited = PManager.Wait(1, pid, out int exitCode);

        Check(waited, "parent waits for crashed child");
        Check(exitCode == 1, "crashed process exits with error code 1");
        Check(PManager.Get(pid) == null, "crashed child is reaped");
    }

    private static void TestKill()
    {
        int pid = PManager.Start("test-kill", [], (procPid, args) =>
        {
            while (!PManager.IsKillReq(procPid)) Thread.Sleep(2);
        });

        Check(PManager.Kill(pid), "kill request accepted");

        bool waited = PManager.Wait(1, pid, out int exitCode);
        Check(waited, "parent waits for killed child");
        Check(exitCode == 137, "killed process exits with code 137");
        Check(PManager.Get(pid) == null, "killed child is reaped");
    }

    private static void TestWaitKill()
    {
        int childPid = PManager.Start("test-wait-target", [], (procPid, args) =>
        {
            while (!PManager.IsKillReq(procPid)) Thread.Sleep(2);
        });

        int waiterPid = PManager.Start("test-waiter", [], (procPid, args) => PManager.Wait(1, childPid, out _, procPid));

        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(waiterPid);
            if (p != null && p.Value.State == PState.Running) break;
            Thread.Sleep(2);
        }

        Check(PManager.Kill(waiterPid), "waiter process can be killed");

        bool waitedWaiter = PManager.Wait(1, waiterPid, out int waiterExitCode);
        Check(waitedWaiter, "killed waiter exits");
        Check(waiterExitCode == 137, "killed waiter exits with code 137");

        PManager.Kill(childPid);
        PManager.Wait(1, childPid, out _);
    }

    private static void TestWaitAndReap()
    {
        int pid = PManager.Start("test-reap", [], (procPid, args) => Thread.Sleep(10));
        bool waited = PManager.Wait(1, pid, out int exitCode);

        Check(waited, "wait blocks until child exits");
        Check(exitCode == 0, "wait receives child exit code");
        Check(PManager.Get(pid) == null, "wait removes child");
        Check(!PManager.Reap(1, pid, out _), "reaped child cannot be reaped again");
    }

    private static void TestZombieReap()
    {
        int pid = PManager.Start("zombie-test", [], (procPid, _) => { }, 1);
        bool becameZombie = false;

        for (int i = 0; i < 100; i++)
        {
            PInfo? p = PManager.Get(pid);
            if (p != null && p.Value.State == PState.Zombie)
            {
                becameZombie = true;
                break;
            }
            Thread.Sleep(2);
        }

        Check(becameZombie, "process becomes zombie");

        PInfo? zombie = PManager.Get(pid);
        Check(zombie != null && zombie.Value.State == PState.Zombie, "zombie is present in process table");
        Check(!PManager.Kill(pid), "zombie cannot be killed");

        bool reaped = PManager.Reap(1, pid, out int exitCode);
        Check(reaped, "zombie is reaped");
        Check(exitCode == 0, "reaped zombie has exit code 0");
        Check(PManager.Get(pid) == null, "reaped zombie disappears from process table");
    }

    private static void TestReparenting()
    {
        int parentPid = PManager.Start("test-reparent-p", [], (pid, args) =>
        {
            while (!PManager.IsKillReq(pid)) Thread.Sleep(2);
        });

        int childPid = PManager.Start("test-reparent-c", [], (pid, args) =>
        {
            while (!PManager.IsKillReq(pid)) Thread.Sleep(2);
        }, parentPid);

        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(parentPid);
            PInfo? c = PManager.Get(childPid);
            if (p != null && p.Value.State == PState.Running && c != null && c.Value.State == PState.Running)
                break;
            Thread.Sleep(2);
        }

        PInfo? childBefore = PManager.Get(childPid);
        Check(childBefore != null && childBefore.Value.ParentPid == parentPid, "child initially has parent PID");

        PManager.Kill(parentPid);
        bool parentWaited = PManager.Wait(1, parentPid, out _);
        Check(parentWaited, "parent process exits and is reaped");

        PInfo? childAfter = PManager.Get(childPid);
        Check(childAfter != null && childAfter.Value.ParentPid == 1, "child reparented to PID 1 on parent death");

        PManager.Kill(childPid);
        bool waited = PManager.Wait(1, childPid, out _);
        Check(waited, "PID 1 can wait and reap adopted child");
        Check(PManager.Get(childPid) == null, "adopted child removed after reap");
    }

    private static void TestOrphanZombieReap()
    {
        int parentPid = PManager.Start("orphan-parent", [], (pid, args) =>
        {
            while (!PManager.IsKillReq(pid)) Thread.Sleep(2);
        });

        int childPid = PManager.Start("orphan-child", [], (pid, args) => Thread.Sleep(2), parentPid);

        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(parentPid);
            if (p != null && p.Value.State == PState.Running) break;
            Thread.Sleep(2);
        }

        PManager.Kill(parentPid);
        PManager.Wait(1, parentPid, out _);

        bool becameZombie = false;
        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(childPid);
            if (p != null && p.Value.State == PState.Zombie)
            {
                becameZombie = true;
                break;
            }
            Thread.Sleep(2);
        }

        Check(becameZombie, "orphan child becomes zombie");

        PInfo? zombie = PManager.Get(childPid);
        Check(zombie != null && zombie.Value.ParentPid == 1, "orphan zombie has ParentPid == 1");

        bool reaped = PManager.Reap(1, childPid, out int exitCode);
        Check(reaped, "PID 1 successfully reaps orphan zombie");
        Check(exitCode == 0, "reaped orphan zombie exit code is preserved");
        Check(PManager.Get(childPid) == null, "reaped orphan zombie removed from process table");
    }

    private static void TestFullOrphanLifecycle()
    {
        int parentPid = PManager.Start("lifecycle-parent", [], (pid, args) =>
        {
            while (!PManager.IsKillReq(pid)) Thread.Sleep(2);
        });

        int childPid = PManager.Start("lifecycle-child", [], (pid, args) =>
        {
            while (!PManager.IsKillReq(pid)) Thread.Sleep(2);
        }, parentPid);

        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(parentPid);
            PInfo? c = PManager.Get(childPid);
            if (p != null && p.Value.State == PState.Running && c != null && c.Value.State == PState.Running)
                break;
            Thread.Sleep(2);
        }

        JManager.Add(childPid, "lifecycle-job");

        PInfo? childInitial = PManager.Get(childPid);
        Check(childInitial != null && childInitial.Value.ParentPid == parentPid, "lifecycle child initially attached to parent");

        PManager.Kill(parentPid);
        bool parentWaited = PManager.Wait(1, parentPid, out _);
        Check(parentWaited, "lifecycle parent killed and reaped");

        PInfo? childReparented = PManager.Get(childPid);
        Check(childReparented != null && childReparented.Value.ParentPid == 1, "lifecycle child reparented to PID 1");

        int parentJobsUpdated = JManager.Update(parentPid);
        Check(parentJobsUpdated == 0, "dead parent JobManager drops orphaned jobs");

        PManager.Kill(childPid);

        bool childIsZombie = false;
        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(childPid);
            if (p != null && p.Value.State == PState.Zombie)
            {
                childIsZombie = true;
                break;
            }
            Thread.Sleep(2);
        }

        Check(childIsZombie, "reparented child transitions to zombie");

        int orphansReaped = PManager.ReapOrphans(force: true);
        Check(orphansReaped >= 1, "PID 1 automatically reaps orphan zombie");
        Check(PManager.Get(childPid) == null, "reaped orphan removed from process table");
        Check(!JManager.Remove(childPid, out _), "reaped orphan cleaned up from JobManager");
    }

    private static void TestStartFailure()
    {
        PManager.SimulateThreadStartFailure = true;
        try
        {
            int failedPid = PManager.Start("fail-proc", [], (pid, args) => { });
            Check(failedPid <= 0, "thread start failure returns error PID");
            Check(PManager.Get(failedPid) == null, "failed process is removed from process table");
        }
        finally
        {
            PManager.SimulateThreadStartFailure = false;
        }
    }

    private static void TestThreadTracking()
    {
        int pid = PManager.Start("track-proc", [], (procPid, args) =>
        {
            while (!PManager.IsKillReq(procPid)) Thread.Sleep(2);
        });

        bool isRunning = false;
        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(pid);
            if (p != null && p.Value.State == PState.Running)
            {
                isRunning = true;
                break;
            }
            Thread.Sleep(2);
        }

        PInfo? process = PManager.Get(pid);
        Check(isRunning && process != null, "tracked process exists");
        Check(process != null && process.Value.Thread != null, "process has associated thread reference");

        PManager.Kill(pid);
        bool waited = PManager.Wait(1, pid, out _);
        Check(waited, "tracked process can be waited");
        Check(PManager.Get(pid) == null, "tracked process reaped and reference cleaned up");
    }

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