// Manager.cs — CManager: command dispatcher and path resolution
using System;
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class CManager
{
    public static string CurrentDirectory { get; set; } = "/";

    public static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return CurrentDirectory;
        string p = path.Trim();
        if (p.StartsWith('/')) return NormalizePath(p);
        string b = CurrentDirectory.EndsWith('/') ? CurrentDirectory : CurrentDirectory + "/";
        return NormalizePath(b + p);
    }

    public static string NormalizePath(string path)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var norm = new List<string>(parts.Length);
        foreach (string part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (norm.Count > 0) norm.RemoveAt(norm.Count - 1);
            }
            else norm.Add(part);
        }
        return "/" + string.Join('/', norm);
    }

    public static int Execute(string input, int parentPid, out bool background)
    {
        background = false;
        string[] args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0) return 0;

        if (args[^1] == "&")
        {
            background = true;
            Array.Resize(ref args, args.Length - 1);
            if (args.Length == 0) return 0;
        }

        string cmd = args[0].ToLower();
        Action<int, string[]>? handler = cmd switch
        {
            "ls" => Ls.Run,
            "cat" => Cat.Run,
            "cd" => Cd.Run,
            "pwd" => Pwd.Run,
            "touch" => Touch.Run,
            "mkdir" => Mkdir.Run,
            "rm" => Rm.Run,
            "rmdir" => Rmdir.Run,
            "df" => Df.Run,
            "stat" => Stat.Run,
            "uname" => Uname.Run,
            "uptime" => Uptime.Run,
            "free" => Free.Run,
            "ps" => Ps.Run,
            "jobs" => Jobs.Run,
            "sleep" => Sleep.Run,
            "kill" => Kill.Run,
            "wait" => Wait.Run,
            "help" => Help.Run,
            "dmesg" => Dmesg.Run,
            "clear" => Clear.Run,
            "test" => Test.Run,
            _ => null
        };

        if (handler != null)
            return PManager.Start(cmd, args, handler, parentPid);

        Output.WriteLine($"Unknown command: {cmd}");
        return 0;
    }
}