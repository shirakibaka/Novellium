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
    private const int KillCode = 137;
    private const int ErrCode = 1;

    private static readonly List<PInfo> Procs = new();
    private static int NextPid = KernelPid + 1;

    public static int Count { get { lock (Procs) return Procs.Count; } }
    internal static bool SimulateThreadStartFailure = false;
    public static bool AutomaticOrphanReaping { get; set; } = true;

    public static bool Initialize()
    {
        lock (Procs)
        {
            Procs.Clear();
            ReapedExitCodes.Clear();
            NextPid = KernelPid + 1;
            Procs.Add(new PInfo
            {
                Pid = KernelPid,
                ParentPid = 0,
                Name = "kernel",
                State = PState.Running,
                ExitCode = 0,
                KillReq = false,
                IsWaited = false,
                Thread = null
            });
        }
        return true;
    }

    public static int Start(string name, string[] args, Action<int, string[]> entry, int parentPid = KernelPid, bool isWaited = false)
    {
        int pid;
        Thread thread;

        lock (Procs)
        {
            pid = NextPid++;
            thread = new(() =>
            {
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

            Procs.Add(new PInfo
            {
                Pid = pid,
                ParentPid = parentPid,
                Name = name,
                State = PState.Created,
                ExitCode = 0,
                KillReq = false,
                IsWaited = isWaited,
                Thread = thread
            });
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
                for (int i = 0; i < Procs.Count; i++)
                {
                    if (Procs[i].Pid == pid)
                    {
                        Procs.RemoveAt(i);
                        break;
                    }
                }
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
            for (int i = 0; i < Procs.Count; i++)
                if (Procs[i].Pid == pid) return Procs[i];
        }
        return null;
    }

    public static void List()
    {
        List<PInfo> snap;
        lock (Procs) snap = new(Procs);

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
            for (int i = 0; i < Procs.Count; i++)
            {
                if (Procs[i].Pid != pid) continue;

                PInfo p = Procs[i];
                if (p.State is PState.Zombie or PState.Terminated or PState.Failed)
                    return false;

                // Reparent orphan children to PID 1 (kernel) upon parent exit
                for (int j = 0; j < Procs.Count; j++)
                {
                    if (Procs[j].ParentPid == pid)
                    {
                        PInfo child = Procs[j];
                        child.ParentPid = KernelPid;
                        Procs[j] = child;
                        reparented ??= new();
                        reparented.Add(child.Pid);
                    }
                }

                p.ExitCode = exitCode;
                p.State = PState.Zombie;
                Procs[i] = p;
                exited = true;
                break;
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
            for (int i = 0; i < Procs.Count; i++)
            {
                if (Procs[i].Pid != pid) continue;
                PInfo p = Procs[i];
                p.State = state;
                Procs[i] = p;
                return;
            }
        }
    }

    private static readonly Dictionary<int, int> ReapedExitCodes = new();

    public static bool Reap(int parentPid, int pid, out int exitCode)
    {
        exitCode = 0;
        lock (Procs)
        {
            for (int i = 0; i < Procs.Count; i++)
            {
                PInfo p = Procs[i];
                if (p.Pid != pid || p.ParentPid != parentPid || p.State != PState.Zombie)
                    continue;

                exitCode = p.ExitCode;
                ReapedExitCodes[pid] = exitCode;
                Procs.RemoveAt(i);
                return true;
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
            for (int i = Procs.Count - 1; i >= 0; i--)
            {
                PInfo p = Procs[i];
                if (p.Pid != KernelPid && p.ParentPid == KernelPid && p.State == PState.Zombie && !p.IsWaited)
                {
                    reaped ??= new();
                    reaped.Add(p.Pid);
                    ReapedExitCodes[p.Pid] = p.ExitCode;
                    Procs.RemoveAt(i);
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
            for (int i = 0; i < Procs.Count; i++)
            {
                if (Procs[i].Pid == pid)
                {
                    PInfo p = Procs[i];
                    p.IsWaited = isWaited;
                    Procs[i] = p;
                    return;
                }
            }
        }
    }

    public static bool Kill(int pid)
    {
        if (pid == KernelPid) return false;

        lock (Procs)
        {
            for (int i = 0; i < Procs.Count; i++)
            {
                if (Procs[i].Pid != pid) continue;

                PInfo p = Procs[i];
                if (p.State is PState.Zombie or PState.Terminated or PState.Failed)
                    return false;

                p.KillReq = true;
                Procs[i] = p;
                return true;
            }
        }
        return false;
    }

    public static bool IsKillReq(int pid)
    {
        lock (Procs)
        {
            for (int i = 0; i < Procs.Count; i++)
                if (Procs[i].Pid == pid) return Procs[i].KillReq;
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
                if (p == null)
                {
                    lock (Procs)
                    {
                        if (ReapedExitCodes.TryGetValue(pid, out int reapedCode))
                        {
                            exitCode = reapedCode;
                            ReapedExitCodes.Remove(pid);
                            return true;
                        }
                    }
                    return false;
                }
                if (p.Value.ParentPid != parentPid) return false;

                if (p.Value.State == PState.Zombie)
                    return Reap(parentPid, pid, out exitCode);

                if (p.Value.State is PState.Terminated or PState.Failed)
                {
                    exitCode = p.Value.ExitCode != 0 ? p.Value.ExitCode : ErrCode;
                    lock (Procs)
                    {
                        for (int i = 0; i < Procs.Count; i++)
                        {
                            if (Procs[i].Pid == pid)
                            {
                                Procs.RemoveAt(i);
                                break;
                            }
                        }
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