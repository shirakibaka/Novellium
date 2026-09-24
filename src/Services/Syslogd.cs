// Syslogd.cs — System logging daemon (/var/log/syslog)
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.Process;

namespace Novellium.Services;

public enum LogLevel { Debug, Info, Ok, Warning, Error }

public readonly struct LogEntry
{
    public readonly DateTime Timestamp;
    public readonly LogLevel Level;
    public readonly string Facility;
    public readonly string Message;

    public LogEntry(DateTime timestamp, LogLevel level, string facility, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Facility = facility;
        Message = message;
    }

    public override string ToString()
    {
        string lvl = Level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Ok => "OK",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "LOG"
        };
        return $"[{Timestamp.Year:D4}-{Timestamp.Month:D2}-{Timestamp.Day:D2} {Timestamp.Hour:D2}:{Timestamp.Minute:D2}:{Timestamp.Second:D2}] [{lvl,-5}] [{Facility}] {Message}";
    }
}

public static class Syslogd
{
    public const string LogFilePath = "/var/log/syslog";
    private const int MaxRing = 200;

    private static readonly object Lock = new();
    private static readonly Queue<LogEntry> Queue = new();
    private static readonly List<LogEntry> Ring = new();

    public static int Pid { get; private set; } = -1;

    public static bool IsRunning
    {
        get
        {
            if (Pid <= 0) return false;
            PInfo? p = PManager.Get(Pid);
            return p != null && p.Value.State == PState.Running;
        }
    }

    public static int Start()
    {
        if (IsRunning) return Pid;
        Pid = PManager.Start("syslogd", ["syslogd"], Run, PManager.KernelPid);
        return Pid;
    }

    public static void Log(LogLevel level, string facility, string msg)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, facility, msg);
        lock (Lock)
        {
            Queue.Enqueue(entry);
            if (Ring.Count >= MaxRing) Ring.RemoveAt(0);
            Ring.Add(entry);
        }
    }

    public static void Info(string facility, string msg) => Log(LogLevel.Info, facility, msg);
    public static void Warn(string facility, string msg) => Log(LogLevel.Warning, facility, msg);
    public static void Error(string facility, string msg) => Log(LogLevel.Error, facility, msg);
    public static void Ok(string facility, string msg) => Log(LogLevel.Ok, facility, msg);
    public static void Debug(string facility, string msg) => Log(LogLevel.Debug, facility, msg);

    public static IReadOnlyList<LogEntry> GetRecentLogs()
    {
        lock (Lock) return new List<LogEntry>(Ring);
    }

    public static void ClearRingBuffer()
    {
        lock (Lock) Ring.Clear();
    }

    public static void Run(int pid, string[] args)
    {
        EnsureLogFile();
        while (!PManager.IsKillReq(pid))
        {
            FlushQueue();
            Thread.Sleep(200);
        }
        FlushQueue();
    }

    private static void EnsureLogFile()
    {
        try
        {
            if (!VfsManager.TryStat(LogFilePath, out _))
            {
                if (!VfsManager.TryStat("/var/log", out var stat) || !stat.IsDirectory)
                {
                    VfsManager.TryCreateDirectory("/var", (VfsMode)493);
                    VfsManager.TryCreateDirectory("/var/log", (VfsMode)493);
                }
                VfsManager.TryCreateFile(LogFilePath, (VfsMode)420);
            }
        }
        catch { }
    }

    private static void FlushQueue()
    {
        List<LogEntry> entries;
        lock (Lock)
        {
            if (Queue.Count == 0) return;
            entries = new(Queue.Count);
            while (Queue.Count > 0) entries.Add(Queue.Dequeue());
        }

        if (entries.Count == 0) return;

        try
        {
            EnsureLogFile();
            if (VfsManager.TryOpenFile(LogFilePath, out var handle) && handle != null)
            {
                using (handle)
                {
                    handle.TrySeek(0, SeekWhence.End);
                    var sb = new StringBuilder();
                    foreach (var e in entries) sb.AppendLine(e.ToString());
                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    handle.Write(bytes);
                    handle.TryFlush();
                }
            }
            else
            {
                lock (Lock)
                {
                    for (int i = entries.Count - 1; i >= 0; i--)
                        if (Queue.Count < 500) Queue.Enqueue(entries[i]);
                }
            }
        }
        catch { }
    }
}
