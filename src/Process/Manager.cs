// Manager.cs — PManager: process, thread, signal, and orphan zombie management
using System;
using System.Collections.Generic;
using System.Threading;
using Novellium.Commands;
using Novellium.IO;

namespace Novellium.Process;

public static class PManager
{
    public const int KernelPid = 1;
    public const int MaxProcesses = 64;
    private const int KillCode = 137;
    private const int ErrCode = 1;

    private static readonly PInfo?[] Procs = new PInfo?[MaxProcesses];
    private static int NextPid = KernelPid + 1;

    public static int Count
    {
        get
        {
            lock (Procs)
            {
                int count = 0;
                for (int i = 0; i < MaxProcesses; i++)
                    if (Procs[i] != null) count++;
                return count;
            }
        }
    }

    internal static bool SimulateThreadStartFailure = false;
    public static bool AutomaticOrphanReaping { get; set; } = true;

    [ThreadStatic]
    private static int _currentThreadPid;

    public static int CurrentPid => _currentThreadPid != 0 ? _currentThreadPid : KernelPid;

    public static string GetCwd(int pid)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                string cwd = Procs[pid]!.CurrentDirectory;
                return string.IsNullOrEmpty(cwd) ? "/" : cwd;
            }
        }
        return "/";
    }

    public static bool SetCwd(int pid, string cwd)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                Procs[pid]!.CurrentDirectory = cwd;
                return true;
            }
        }
        return false;
    }

    public static int GetParentPid(int pid)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
                return Procs[pid]!.ParentPid;
        }
        return 0;
    }

    public static bool Initialize()
    {
        lock (Procs)
        {
            Array.Clear(Procs, 0, Procs.Length);
            NextPid = KernelPid + 1;
            Procs[KernelPid] = new PInfo
            {
                Pid = KernelPid,
                ParentPid = 0,
                Name = "kernel",
                State = PState.Running,
                ExitCode = 0,
                KillReq = false,
                IsWaited = false,
                Thread = null,
                CurrentDirectory = "/"
            };
        }
        return true;
    }

    public static int Start(string name, string[] args, Action<int, string[]> entry, int parentPid = KernelPid, bool isWaited = false)
    {
        int pid = -1;
        Thread thread;

        lock (Procs)
        {
            for (int attempts = 0; attempts < MaxProcesses - 1; attempts++)
            {
                int candidate = NextPid++;
                if (NextPid >= MaxProcesses) NextPid = KernelPid + 1;

                if (Procs[candidate] == null)
                {
                    pid = candidate;
                    break;
                }
            }

            if (pid == -1) return -1;

            string initialCwd = GetCwd(parentPid);

            thread = new(() =>
            {
                _currentThreadPid = pid;
                SetState(pid, PState.Running);
                try
                {
                    entry(pid, args);
                    Exit(pid, IsKillReq(pid) ? KillCode : 0);
                }
                catch (Exception ex)
                {
                    OutputInfo.Error($"[PROCESS] PID: {pid} NAME: {name} error: {ex.Message}");
                    Exit(pid, ErrCode);
                }
            });

            Procs[pid] = new PInfo
            {
                Pid = pid,
                ParentPid = parentPid,
                Name = name,
                State = PState.Created,
                ExitCode = 0,
                KillReq = false,
                IsWaited = isWaited,
                Thread = thread,
                CurrentDirectory = initialCwd
            };
        }

        try
        {
            if (SimulateThreadStartFailure)
                throw new InvalidOperationException("Simulated thread start failure");

            thread.Start();
        }
        catch (Exception ex)
        {
            lock (Procs)
            {
                if (pid >= 0 && pid < MaxProcesses) Procs[pid] = null;
            }

            if (!SimulateThreadStartFailure)
                OutputInfo.Error($"[PROCESS] Failed to start thread for PID {pid} NAME {name}: {ex.Message}");
            return -1;
        }

        return pid;
    }

    public static PInfo? Get(int pid)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses) return Procs[pid];
            return null;
        }
    }

    public static void List()
    {
        List<PInfo> snap = new();
        lock (Procs)
        {
            for (int i = 0; i < MaxProcesses; i++)
                if (Procs[i] != null) snap.Add(Procs[i]!);
        }

        Output.WriteLine($"{"PID",-6}{"PPID",-6}{"THREAD",-8}{"STATE",-12}{"EXIT",-7}NAME");
        foreach (PInfo p in snap)
        {
            string th = p.Thread != null ? $"TID:{p.Thread.ManagedThreadId}" : "-";
            Output.WriteLine($"{p.Pid,-6}{p.ParentPid,-6}{th,-8}{p.State,-12}{p.ExitCode,-7}{p.Name}");
        }
    }

    public static bool Exit(int pid, int exitCode)
    {
        bool exited = false;
        List<int>? reparented = null;

        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                PInfo p = Procs[pid]!;
                if (p.State is PState.Zombie or PState.Terminated or PState.Failed)
                    return false;

                // Reparent orphan children to PID 1 (kernel) upon parent exit
                for (int j = 0; j < MaxProcesses; j++)
                {
                    if (Procs[j] != null && Procs[j]!.ParentPid == pid)
                    {
                        Procs[j]!.ParentPid = KernelPid;
                        reparented ??= new();
                        reparented.Add(j);
                    }
                }

                p.ExitCode = exitCode;
                p.State = PState.Zombie;
                exited = true;
            }
        }

        if (exited && reparented != null)
        {
            foreach (int childPid in reparented)
            {
                try { JManager.Remove(childPid, out _); } catch { }
            }
        }

        return exited;
    }

    private static void SetState(int pid, PState state)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                Procs[pid]!.State = state;
            }
        }
    }

    public static bool Reap(int parentPid, int pid, out int exitCode)
    {
        exitCode = 0;
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                PInfo p = Procs[pid]!;
                if (p.ParentPid == parentPid && p.State == PState.Zombie)
                {
                    exitCode = p.ExitCode;
                    Procs[pid] = null;
                    return true;
                }
            }
        }
        return false;
    }

    public static int ReapOrphans(bool force = false)
    {
        if (!AutomaticOrphanReaping && !force) return 0;

        int count = 0;
        List<int>? reaped = null;

        lock (Procs)
        {
            for (int i = 0; i < MaxProcesses; i++)
            {
                PInfo? p = Procs[i];
                if (p != null && p.Pid != KernelPid && p.ParentPid == KernelPid && p.State == PState.Zombie && !p.IsWaited)
                {
                    reaped ??= new();
                    reaped.Add(p.Pid);
                    Procs[i] = null;
                    count++;
                }
            }
        }

        if (reaped != null)
        {
            foreach (int pid in reaped)
            {
                try { JManager.Remove(pid, out _); } catch { }
            }
        }

        return count;
    }

    public static void SetWaited(int pid, bool isWaited)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                Procs[pid]!.IsWaited = isWaited;
            }
        }
    }

    public static bool Kill(int pid)
    {
        if (pid == KernelPid) return false;

        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
            {
                PInfo p = Procs[pid]!;
                if (p.State is PState.Zombie or PState.Terminated or PState.Failed)
                    return false;

                p.KillReq = true;
                return true;
            }
        }
        return false;
    }

    public static bool IsKillReq(int pid)
    {
        lock (Procs)
        {
            if (pid >= 0 && pid < MaxProcesses && Procs[pid] != null)
                return Procs[pid]!.KillReq;
        }
        return false;
    }

    public static bool Wait(int parentPid, int pid, out int exitCode, int callerPid = 0, int timeoutMs = -1)
    {
        exitCode = 0;
        int checkPid = callerPid != 0 ? callerPid : parentPid;

        SetWaited(pid, true);
        try
        {
            long startTick = Environment.TickCount64;
            while (timeoutMs < 0 || (Environment.TickCount64 - startTick) < timeoutMs)
            {
                if (checkPid > 0 && IsKillReq(checkPid)) return false;

                PInfo? p = Get(pid);
                if (p == null) return false;
                if (p.ParentPid != parentPid) return false;

                if (p.State == PState.Zombie)
                    return Reap(parentPid, pid, out exitCode);

                if (p.State is PState.Terminated or PState.Failed)
                {
                    exitCode = p.ExitCode != 0 ? p.ExitCode : ErrCode;
                    lock (Procs)
                    {
                        if (pid >= 0 && pid < MaxProcesses) Procs[pid] = null;
                    }
                    return true;
                }

                Thread.Sleep(5);
            }
            return false;
        }
        finally
        {
            SetWaited(pid, false);
        }
    }
}