// Manager.cs — CManager: command dispatcher, path resolution, redirection & pipelines
using System;
using System.Collections.Generic;
using System.Text;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
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

    public static string ReadFileText(string path)
    {
        string full = ResolvePath(path);
        if (!VfsManager.TryStat(full, out VfsStat st) || st.IsDirectory) return string.Empty;
        if (!VfsManager.TryOpenFile(full, out var h) || h == null) return string.Empty;
        using (h)
        {
            byte[] buf = new byte[1024];
            StringBuilder sb = new();
            long read;
            while ((read = h.Read(buf)) > 0)
                sb.Append(Encoding.UTF8.GetString(buf, 0, (int)read));
            return sb.ToString();
        }
    }

    public static bool WriteFileText(string path, string text, bool append)
    {
        string full = ResolvePath(path);
        if (!append && VfsManager.TryStat(full, out _))
        {
            VfsManager.TryUnlink(full);
        }
        if (!VfsManager.TryStat(full, out _))
        {
            if (!VfsManager.TryCreateFile(full, (VfsMode)420)) return false;
        }
        if (!VfsManager.TryOpenFile(full, out var h) || h == null) return false;
        using (h)
        {
            if (append) h.TrySeek(0, SeekWhence.End);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            h.Write(bytes);
            h.TryFlush();
        }
        return true;
    }

    public static string[] SplitArgs(string input)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        bool inDouble = false, inSingle = false;
        bool tokenActive = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '"' && !inSingle)
            {
                inDouble = !inDouble;
                tokenActive = true;
            }
            else if (c == '\'' && !inDouble)
            {
                inSingle = !inSingle;
                tokenActive = true;
            }
            else if (char.IsWhiteSpace(c) && !inDouble && !inSingle)
            {
                if (tokenActive)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    tokenActive = false;
                }
            }
            else
            {
                current.Append(c);
                tokenActive = true;
            }
        }
        if (tokenActive) args.Add(current.ToString());
        return args.ToArray();
    }

    public static int Execute(string input, int parentPid, out bool background)
    {
        background = false;
        if (string.IsNullOrWhiteSpace(input)) return 0;

        string trimmed = input.Trim();

        List<string> stages = SplitPipeline(trimmed);
        if (stages.Count > 1)
        {
            string? pipeStdin = null;
            int lastPid = 0;
            for (int s = 0; s < stages.Count; s++)
            {
                bool isLast = (s == stages.Count - 1);
                lastPid = ExecuteStage(stages[s], parentPid, out background, pipeStdin, captureStdout: !isLast, out string stageOut);
                pipeStdin = stageOut;
            }
            return lastPid;
        }

        return ExecuteStage(trimmed, parentPid, out background, stdinText: null, captureStdout: false, out _);
    }

    private static int ExecuteStage(string stageStr, int parentPid, out bool background, string? stdinText, bool captureStdout, out string capturedOut)
    {
        capturedOut = "";
        background = false;

        ParseRedirection(stageStr, out string cleanCmd, out string? inFile, out string? outFile, out bool append);

        if (inFile != null)
        {
            stdinText = ReadFileText(inFile);
        }

        if (outFile != null)
        {
            captureStdout = true;
        }

        string[] args = SplitArgs(cleanCmd);
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
            string a = args[i];
            bool isHelp = (cmdName is "df" or "free") ? (a == "--help") : (a is "-h" or "--help");
            if (isHelp)
            {
                entry.HelpHandler();
                return 0;
            }
        }

        int pid = 0;
        if (entry.IsBuiltin)
        {
            PInfo? parentProc = PManager.Get(parentPid);
            if (parentProc != null)
            {
                if (stdinText != null) parentProc.StdinText = stdinText;
                if (captureStdout) parentProc.StdoutBuffer = new StringBuilder();
            }
            try
            {
                entry.Handler(parentPid, args);
                if (captureStdout && parentProc?.StdoutBuffer != null)
                {
                    capturedOut = parentProc.StdoutBuffer.ToString();
                    parentProc.StdoutBuffer = null;
                }
            }
            finally
            {
                if (parentProc != null) parentProc.StdinText = "";
            }
        }
        else
        {
            pid = PManager.Start(entry.Name, args, entry.Handler, parentPid, isWaited: !background, stdinText: stdinText, captureStdout: captureStdout);
            if (pid > 0 && !background)
            {
                PManager.Wait(parentPid, pid, out _);
                if (captureStdout) capturedOut = PManager.GetOutput(pid);
            }
        }

        if (outFile != null)
        {
            WriteFileText(outFile, capturedOut, append);
        }

        return pid;
    }

    private static List<string> SplitPipeline(string input)
    {
        var stages = new List<string>();
        var current = new StringBuilder();
        bool inDouble = false, inSingle = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '|' && !inDouble && !inSingle)
            {
                stages.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(c);
        }
        if (current.Length > 0) stages.Add(current.ToString().Trim());
        return stages;
    }

    private static void ParseRedirection(string cmdStr, out string cleanCmd, out string? inFile, out string? outFile, out bool append)
    {
        inFile = null;
        outFile = null;
        append = false;

        var sb = new StringBuilder();
        bool inDouble = false, inSingle = false;

        for (int i = 0; i < cmdStr.Length; i++)
        {
            char c = cmdStr[i];
            if (c == '"' && !inSingle) { inDouble = !inDouble; sb.Append(c); }
            else if (c == '\'' && !inDouble) { inSingle = !inSingle; sb.Append(c); }
            else if (c == '>' && !inDouble && !inSingle)
            {
                append = (i + 1 < cmdStr.Length && cmdStr[i + 1] == '>');
                int start = append ? i + 2 : i + 1;
                outFile = ReadToken(cmdStr, ref start);
                i = start - 1;
            }
            else if (c == '<' && !inDouble && !inSingle)
            {
                int start = i + 1;
                inFile = ReadToken(cmdStr, ref start);
                i = start - 1;
            }
            else sb.Append(c);
        }

        cleanCmd = sb.ToString().Trim();
    }

    private static string ReadToken(string str, ref int index)
    {
        while (index < str.Length && char.IsWhiteSpace(str[index])) index++;
        var sb = new StringBuilder();
        bool inDouble = false, inSingle = false;
        while (index < str.Length)
        {
            char c = str[index];
            if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if ((char.IsWhiteSpace(c) || c == '>' || c == '<' || c == '|') && !inDouble && !inSingle) break;
            else sb.Append(c);
            index++;
        }
        return sb.ToString();
    }
}