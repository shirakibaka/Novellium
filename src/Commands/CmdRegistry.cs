// CmdRegistry.cs — Centralized command registry
using System;
using System.Collections.Generic;

namespace Novellium.Commands;

public class CmdEntry
{
    public string Name { get; }
    public string Synopsis { get; }
    public string Summary { get; }
    public Action<int, string[]> Handler { get; }
    public Action HelpHandler { get; }
    public bool IsBuiltin { get; }

    public CmdEntry(string name, string synopsis, string summary, Action<int, string[]> handler, Action helpHandler, bool isBuiltin = false)
    {
        Name = name;
        Synopsis = synopsis;
        Summary = summary;
        Handler = handler;
        HelpHandler = helpHandler;
        IsBuiltin = isBuiltin;
    }
}

public static class CmdRegistry
{
    private static readonly Dictionary<string, CmdEntry> Commands = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<CmdEntry> CommandList = new();

    static CmdRegistry()
    {
        Register(new CmdEntry("help", "help [command]", "display information about builtin commands", Help.Run, Help.Show, isBuiltin: true));
        Register(new CmdEntry("ls", "ls [options] [path]", "list directory contents", Ls.Run, Ls.Help));
        Register(new CmdEntry("cat", "cat [options] [file]", "concatenate and display files", Cat.Run, Cat.Help));
        Register(new CmdEntry("cd", "cd [directory]", "change the current working directory", Cd.Run, Cd.Help, isBuiltin: true));
        Register(new CmdEntry("pwd", "pwd", "print the current working directory", Pwd.Run, Pwd.Help, isBuiltin: true));
        Register(new CmdEntry("touch", "touch [file]...", "create empty file or update timestamp", Touch.Run, Touch.Help));
        Register(new CmdEntry("mkdir", "mkdir [-p] [dir]...", "create directory", Mkdir.Run, Mkdir.Help));
        Register(new CmdEntry("rm", "rm [-f] [file]...", "remove (unlink) file", Rm.Run, Rm.Help));
        Register(new CmdEntry("rmdir", "rmdir [dir]...", "remove empty directory", Rmdir.Run, Rmdir.Help));
        Register(new CmdEntry("df", "df [-h]", "show filesystem disk space usage", Df.Run, Df.Help));
        Register(new CmdEntry("stat", "stat [file]...", "display file or filesystem status", Stat.Run, Stat.Help));
        Register(new CmdEntry("uname", "uname [-a]", "print system and kernel information", Uname.Run, Uname.Help));
        Register(new CmdEntry("uptime", "uptime", "show system uptime and process count", Uptime.Run, Uptime.Help));
        Register(new CmdEntry("free", "free [-h|-m|-k]", "display memory and heap usage", Free.Run, Free.Help));
        Register(new CmdEntry("ps", "ps", "report snapshot of current processes", Ps.Run, Ps.Help));
        Register(new CmdEntry("jobs", "jobs", "list active background jobs", Jobs.Run, Jobs.Help, isBuiltin: true));
        Register(new CmdEntry("kill", "kill <pid>", "terminate a process by PID", Kill.Run, Kill.Help));
        Register(new CmdEntry("wait", "wait <pid>", "wait for a child process to terminate", Wait.Run, Wait.Help));
        Register(new CmdEntry("sleep", "sleep <seconds>", "delay for a specified number of seconds", Sleep.Run, Sleep.Help));
        Register(new CmdEntry("dmesg", "dmesg [options]", "print system and kernel log buffer", Dmesg.Run, Dmesg.Help));
        Register(new CmdEntry("clear", "clear", "clear the terminal screen", Clear.Run, Clear.Help, isBuiltin: true));
        Register(new CmdEntry("test", "test [suite]", "run kernel and command automated test suites", Test.Run, Test.Help));
    }

    private static void Register(CmdEntry entry)
    {
        Commands[entry.Name] = entry;
        CommandList.Add(entry);
    }

    public static CmdEntry? Get(string name)
    {
        if (Commands.TryGetValue(name, out var entry)) return entry;
        return null;
    }

    public static IReadOnlyList<CmdEntry> GetAll() => CommandList;
}
