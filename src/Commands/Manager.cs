// Manager.cs — CManager: command dispatcher and path resolution
using System;
using System.Collections.Generic;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class CManager
{
    public static string CurrentDirectory
    {
        get => PManager.GetCwd(PManager.CurrentPid);
        set => PManager.SetCwd(PManager.CurrentPid, value);
    }

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
        if (string.IsNullOrWhiteSpace(input)) return 0;

        string[] args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0) return 0;

        if (args[^1] == "&")
        {
            background = true;
            Array.Resize(ref args, args.Length - 1);
            if (args.Length == 0) return 0;
        }

        string cmdName = args[0].ToLower();
        CmdEntry? entry = CmdRegistry.Get(cmdName);
        if (entry == null)
        {
            Output.WriteLine($"Unknown command: {cmdName}");
            return 0;
        }

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "-h" or "--help")
            {
                entry.HelpHandler();
                return 0;
            }
        }

        if (entry.IsBuiltin)
        {
            entry.Handler(parentPid, args);
            return 0;
        }

        return PManager.Start(entry.Name, args, entry.Handler, parentPid, isWaited: !background);
    }
}