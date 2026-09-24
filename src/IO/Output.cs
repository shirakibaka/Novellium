// Output.cs — Thread-safe console I/O and formatted diagnostic logging
using System;
using System.Text;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.IO;

public static class Output
{
    private static readonly object Lock = new();
    private static bool IsCapturing = false;
    private static readonly StringBuilder CapturedBuffer = new();

    public static string GetStdin()
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        return proc?.StdinText ?? string.Empty;
    }

    public static void StartCapture()
    {
        lock (Lock)
        {
            CapturedBuffer.Clear();
            IsCapturing = true;
        }
    }

    public static string StopCapture()
    {
        lock (Lock)
        {
            IsCapturing = false;
            string text = CapturedBuffer.ToString();
            CapturedBuffer.Clear();
            return text;
        }
    }

    public static bool IsCaptureActive
    {
        get { lock (Lock) return IsCapturing; }
    }

    public static void WriteDirectLine(string text = "", ConsoleColor? color = null)
    {
        lock (Lock)
        {
            if (color.HasValue)
            {
                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = color.Value;
                Console.WriteLine(text);
                Console.ForegroundColor = prev;
            }
            else
            {
                Console.WriteLine(text);
            }
        }
    }

    public static void Write(string text)
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null)
        {
            lock (proc.StdoutBuffer) proc.StdoutBuffer.Append(text);
            return;
        }

        lock (Lock)
        {
            if (IsCapturing) CapturedBuffer.Append(text);
            else Console.Write(text);
        }
    }

    public static void WriteLine(string text)
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null)
        {
            lock (proc.StdoutBuffer) proc.StdoutBuffer.AppendLine(text);
            return;
        }

        lock (Lock)
        {
            if (IsCapturing) CapturedBuffer.AppendLine(text);
            else Console.WriteLine(text);
        }
    }

    public static void WriteLine()
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null)
        {
            lock (proc.StdoutBuffer) proc.StdoutBuffer.AppendLine();
            return;
        }

        lock (Lock)
        {
            if (IsCapturing) CapturedBuffer.AppendLine();
            else Console.WriteLine();
        }
    }

    public static void Write(string text, ConsoleColor color)
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null)
        {
            lock (proc.StdoutBuffer) proc.StdoutBuffer.Append(text);
            return;
        }

        lock (Lock)
        {
            if (IsCapturing) CapturedBuffer.Append(text);
            else
            {
                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(text);
                Console.ForegroundColor = prev;
            }
        }
    }

    public static void WriteLine(string text, ConsoleColor color)
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null)
        {
            lock (proc.StdoutBuffer) proc.StdoutBuffer.AppendLine(text);
            return;
        }

        lock (Lock)
        {
            if (IsCapturing) CapturedBuffer.AppendLine(text);
            else
            {
                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = prev;
            }
        }
    }

    public static void ResetColor()
    {
        lock (Lock) Console.ResetColor();
    }

    public static void Clear()
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null) return;

        lock (Lock)
        {
            if (!IsCapturing) Console.Clear();
        }
    }

    public static void WriteTag(string tag, ConsoleColor tagColor, string text, ConsoleColor? textColor = null)
    {
        PInfo? proc = PManager.Get(PManager.CurrentPid);
        if (proc?.StdoutBuffer != null)
        {
            Write($"[{tag}] {text}\n");
            return;
        }

        lock (Lock)
        {
            if (IsCapturing)
            {
                CapturedBuffer.AppendLine($"[{tag}] {text}");
                return;
            }

            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("[");
            Console.ForegroundColor = tagColor;
            Console.Write(tag);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("] ");
            if (textColor.HasValue) Console.ForegroundColor = textColor.Value;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }
    }

    public static void WriteTaggedLine(string tag, ConsoleColor tagColor, string text, ConsoleColor? textColor = null)
        => WriteTag(tag, tagColor, text, textColor);
}

public static class OutputInfo
{
    public static void Ok(string text)
    {
        Output.WriteTag("OK", ConsoleColor.Green, text);
        Syslogd.Log(LogLevel.Ok, "system", text);
    }

    public static void Error(string text)
    {
        Output.WriteTag("ERROR", ConsoleColor.Red, text);
        Syslogd.Log(LogLevel.Error, "system", text);
    }

    public static void Warning(string text)
    {
        Output.WriteTag("WARNING", ConsoleColor.Yellow, text);
        Syslogd.Log(LogLevel.Warning, "system", text);
    }

    public static void Info(string text)
    {
        Output.WriteTag("INFO", ConsoleColor.Cyan, text);
        Syslogd.Log(LogLevel.Info, "system", text);
    }

    public static void Debug(string text)
    {
        Output.WriteTag("DEBUG", ConsoleColor.Magenta, text);
        Syslogd.Log(LogLevel.Debug, "system", text);
    }

    public static void Custom(string title, string text, ConsoleColor titleColor, ConsoleColor textColor)
        => Output.WriteTag(title, titleColor, text, textColor);

    public static void Test(bool passed, string text)
        => Output.WriteTag(passed ? "PASS" : "FAIL", passed ? ConsoleColor.Green : ConsoleColor.Red, text);

    public static void TestWarning(string text)
        => Output.WriteTag("WARN", ConsoleColor.Yellow, text);
}
