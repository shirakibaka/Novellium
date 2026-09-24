// JManager.cs — Shell background job manager for novsh
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class JManager
{
    private struct Job { public int Id, Pid; public string Name; public bool IsWaited; }

    private static readonly object Lock = new();
    private static readonly List<Job> Jobs = new();
    private static int NextId = 1;

    public static void Init()
    {
        lock (Lock) { Jobs.Clear(); NextId = 1; }
    }

    public static void Add(int pid, string name)
    {
        int id;
        lock (Lock)
        {
            id = NextId++;
            Jobs.Add(new Job { Id = id, Pid = pid, Name = name, IsWaited = false });
        }
        Output.WriteLine($"[{id}] {pid} started");
    }

    public static bool SetWaited(int pid, bool waited)
    {
        lock (Lock)
        {
            for (int i = 0; i < Jobs.Count; i++)
            {
                if (Jobs[i].Pid == pid)
                {
                    Job j = Jobs[i];
                    j.IsWaited = waited;
                    Jobs[i] = j;
                    return true;
                }
            }
            return false;
        }
    }

    public static bool Remove(int pid, out int id)
    {
        lock (Lock)
        {
            for (int i = 0; i < Jobs.Count; i++)
            {
                if (Jobs[i].Pid == pid)
                {
                    id = Jobs[i].Id;
                    Jobs.RemoveAt(i);
                    return true;
                }
            }
            id = 0;
            return false;
        }
    }

    public static int Update(int parentPid)
    {
        List<Job> snap;
        lock (Lock) snap = new(Jobs);

        List<(int Id, int Pid, int Code)>? done = null;
        foreach (Job j in snap)
        {
            if (j.IsWaited) continue;

            PInfo? p = PManager.Get(j.Pid);
            if (p == null || p.ParentPid != parentPid)
            {
                Remove(j.Pid, out _);
                continue;
            }

            if (p.State != PState.Zombie) continue;
            if (!PManager.Reap(parentPid, j.Pid, out int code)) continue;

            done ??= new();
            done.Add((j.Id, j.Pid, code));
            Remove(j.Pid, out _);
        }

        if (done == null) return 0;
        foreach (var item in done)
            Output.WriteLine($"[{item.Id}] {item.Pid} done ({item.Code})");
        return done.Count;
    }

    public static void List()
    {
        List<Job> snap;
        lock (Lock) snap = new(Jobs);

        foreach (Job j in snap)
        {
            PInfo? p = PManager.Get(j.Pid);
            if (p != null) Output.WriteLine($"[{j.Id}] {j.Pid} {p.State} {j.Name}");
        }
    }
}