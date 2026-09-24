// CTest.cs — Core shell command test suite
using System;
using System.Collections.Generic;
using System.Threading;
using Novellium.Commands;
using Novellium.IO;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.Tests;

public static class CommandTests
{
    private static int Passed, Failed;
    private static readonly List<string> FailedTests = new();
    private static TestBlock? CurBlock;

    private class TestBlock
    {
        public string Title { get; }
        public List<(bool Passed, string Name)> Items { get; } = new();

        public TestBlock(string title)
        {
            Title = title;
        }
    }

    private static int Exec(string cmd)
    {
        int pid = CManager.Execute(cmd, 1, out _);
        if (pid <= 0) return -1;
        if (!PManager.Wait(1, pid, out int code, timeoutMs: 15000))
        {
            PManager.Kill(pid);
            PManager.Wait(1, pid, out _);
            return -1;
        }
        return code;
    }

    public static void Run()
    {
        Passed = 0; Failed = 0; FailedTests.Clear();
        Output.WriteDirectLine("=== COMMAND TESTS ===", ConsoleColor.Cyan);

        List<TestBlock> blocks = new();
        Output.StartCapture();
        try
        {
            blocks.Add(TestUnknownCommand());
            blocks.Add(TestForeground());
            blocks.Add(TestBackground());
            blocks.Add(TestLsCommand());
            blocks.Add(TestFilesystemCommands());
            blocks.Add(TestSystemInfoCommands());
            blocks.Add(TestHelpCommand());
            blocks.Add(TestCommandHelpFlags());
            blocks.Add(TestDmesgAndSyslog());
            blocks.Add(TestClearCommand());
        }
        finally
        {
            Output.StopCapture();
        }

        Output.WriteDirectLine();
        for (int i = 0; i < blocks.Count; i += 2)
        {
            TestBlock left = blocks[i];
            TestBlock? right = i + 1 < blocks.Count ? blocks[i + 1] : null;
            PrintBlockPair(left, right);
        }

        ComprehensiveCommandTests.Run();

        Output.WriteDirectLine();
        if (Failed == 0)
        {
            Output.WriteDirectLine($"RESULT: {Passed} passed", ConsoleColor.Green);
        }
        else
        {
            Output.WriteDirectLine($"RESULT: {Passed} passed, {Failed} failed", ConsoleColor.Red);
            Output.WriteDirectLine("Failed tests:", ConsoleColor.Red);
            foreach (string fail in FailedTests) Output.WriteDirectLine($"  - {fail}", ConsoleColor.Red);
        }
        Output.WriteDirectLine();
    }

    private static string PadOrTruncate(string str, int width)
    {
        if (str.Length > width) return str.Substring(0, width - 3) + "...";
        return str.PadRight(width);
    }

    private static void PrintBlockPair(TestBlock left, TestBlock? right)
    {
        const int width = 38;
        int maxItems = Math.Max(left.Items.Count, right?.Items.Count ?? 0);

        string leftHdr = PadOrTruncate($"--- {left.Title} ---", width);
        string rightHdr = right != null ? PadOrTruncate($"--- {right.Title} ---", width) : "";

        Output.Write(leftHdr, ConsoleColor.Yellow);
        Output.Write("   ");
        Output.WriteLine(rightHdr, ConsoleColor.Yellow);

        for (int i = 0; i < maxItems; i++)
        {
            if (i < left.Items.Count)
            {
                var (passed, name) = left.Items[i];
                Output.Write("[", ConsoleColor.White);
                Output.Write(passed ? "PASS" : "FAIL", passed ? ConsoleColor.Green : ConsoleColor.Red);
                Output.Write("] ", ConsoleColor.White);
                Output.Write(PadOrTruncate(name, width - 7));
            }
            else
            {
                Output.Write(new string(' ', width));
            }

            Output.Write("   ");

            if (right != null && i < right.Items.Count)
            {
                var (passed, name) = right.Items[i];
                Output.Write("[", ConsoleColor.White);
                Output.Write(passed ? "PASS" : "FAIL", passed ? ConsoleColor.Green : ConsoleColor.Red);
                Output.Write("] ", ConsoleColor.White);
                Output.WriteLine(PadOrTruncate(name, width - 7));
            }
            else
            {
                Output.WriteLine();
            }
        }
        Output.WriteLine();
    }

    private static TestBlock TestUnknownCommand()
    {
        TestBlock block = new("Unknown Command Tests");
        CurBlock = block;
        int pid = CManager.Execute("doesnotexist", 1, out bool bg);
        Check(pid == 0, "unknown command");
        Check(!bg, "unknown command is not background");
        return block;
    }

    private static TestBlock TestForeground()
    {
        TestBlock block = new("Foreground Process Tests");
        CurBlock = block;
        int pid = CManager.Execute("sleep 0", 1, out bool bg);
        Check(pid > 0, "foreground command returns PID");
        Check(!bg, "foreground command detected");

        bool waited = PManager.Wait(1, pid, out int exitCode);
        Check(waited, "foreground command waits");
        Check(exitCode == 0, "foreground command exits with code 0");
        Check(PManager.Get(pid) == null, "foreground process is reaped");
        return block;
    }

    private static TestBlock TestBackground()
    {
        TestBlock block = new("Background Process Tests");
        CurBlock = block;
        int pid = CManager.Execute("sleep 0 &", 1, out bool bg);
        Check(pid > 0, "background command returns PID");
        Check(bg, "background command detected");

        bool exists = false, isRunning = false;
        for (int i = 0; i < 50; i++)
        {
            PInfo? p = PManager.Get(pid);
            if (p != null)
            {
                exists = true;
                if (p.Value.State is PState.Running or PState.Zombie)
                {
                    isRunning = true;
                    break;
                }
            }
            Thread.Sleep(2);
        }

        Check(exists, "background process exists");
        Check(isRunning, "background process is running");

        bool waited = PManager.Wait(1, pid, out int exitCode);
        Check(waited, "background process can be waited");
        Check(exitCode == 0, "background process exits with code 0");
        Check(PManager.Get(pid) == null, "background process is reaped");
        return block;
    }

    private static TestBlock TestLsCommand()
    {
        TestBlock block = new("ls Command Tests");
        CurBlock = block;
        int pid = CManager.Execute("ls", 1, out bool bg);
        Check(pid > 0 && !bg, "ls command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "ls exits with 0");

        Check(Exec("ls -l /etc") == 0, "ls -l /etc executes");
        Check(Exec("ls -a /bin") == 0, "ls -a /bin executes");
        Check(Exec("ls -1 /tmp") == 0, "ls -1 /tmp executes");
        Check(Exec("ls --help") == 0, "ls --help executes");
        Check(Exec("ls -z") >= 0, "ls -z executes");
        return block;
    }

    private static TestBlock TestFilesystemCommands()
    {
        TestBlock block = new("Filesystem Commands Tests");
        CurBlock = block;
        Check(Exec("pwd") == 0, "pwd command executes");
        Check(Exec("touch /tmp/ctest.txt") == 0, "touch command executes");
        Check(Exec("stat /tmp/ctest.txt") == 0, "stat command executes");
        Check(Exec("cat /etc/hostname") == 0, "cat command executes");
        Check(Exec("mkdir -p /tmp/testdir/sub") == 0, "mkdir command executes");
        Check(Exec("cd /tmp") == 0 && CManager.CurrentDirectory == "/tmp", "cd changes directory");
        Exec("cd /");
        Check(CManager.CurrentDirectory == "/", "cd returns to root");
        Check(Exec("rm /tmp/ctest.txt") == 0, "rm command executes");
        Check(Exec("rmdir /tmp/testdir/sub") == 0, "rmdir command executes");
        Check(Exec("df -h") == 0, "df command executes");
        return block;
    }

    private static TestBlock TestSystemInfoCommands()
    {
        TestBlock block = new("System Info Commands Tests");
        CurBlock = block;
        Check(Exec("uname -a") == 0, "uname command executes");
        Check(Exec("uptime") == 0, "uptime command executes");
        Check(Exec("free -m") == 0, "free command executes");
        return block;
    }

    private static TestBlock TestHelpCommand()
    {
        TestBlock block = new("help Command Tests");
        CurBlock = block;
        int pid = CManager.Execute("help", 1, out bool bg);
        Check(pid > 0 && !bg, "help command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "help exits with 0");

        string[] topics = ["ls", "cat", "cd", "pwd", "touch", "mkdir", "rm", "rmdir", "df", "stat", "uname", "uptime", "free", "ps", "jobs", "kill", "wait", "sleep", "help", "dmesg", "clear", "test"];
        bool allOk = true;
        foreach (string t in topics)
        {
            if (Exec($"help {t}") != 0) { allOk = false; break; }
        }
        Check(allOk, "help for all builtin topics succeeds");

        int unkPid = CManager.Execute("help nonexistenttopic", 1, out _);
        Check(unkPid > 0, "help unknown topic executes");
        PManager.Wait(1, unkPid, out _);
        return block;
    }

    private static TestBlock TestCommandHelpFlags()
    {
        TestBlock block = new("Command Help Flags Tests");
        CurBlock = block;
        string[] commands = [
            "ps --help", "jobs -h", "kill --help", "wait -h", "sleep --help",
            "dmesg -h", "cat --help", "cd -h", "pwd --help", "touch -h",
            "mkdir --help", "rm -h", "rmdir --help", "stat --help", "uname -h",
            "uptime --help", "free -h", "clear -h", "clear --help", "test -h", "test --help"
        ];
        bool allOk = true;
        foreach (string cmd in commands)
        {
            if (Exec(cmd) != 0) { allOk = false; break; }
        }
        Check(allOk, "all commands support -h/--help flags");
        return block;
    }

    private static TestBlock TestDmesgAndSyslog()
    {
        TestBlock block = new("dmesg & syslog Tests");
        CurBlock = block;
        int pid = CManager.Execute("dmesg", 1, out bool bg);
        Check(pid > 0 && !bg, "dmesg command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "dmesg exits with 0");
        Check(Exec("dmesg -h") == 0, "dmesg -h executes");

        Syslogd.Info("test", "test syslogd log entry");
        var recent = Syslogd.GetRecentLogs();
        Check(recent.Count > 0, "syslogd buffer captures log entry");

        int sysPid = Syslogd.Start();
        Check(sysPid > 0, "syslogd daemon started");
        return block;
    }

    private static TestBlock TestClearCommand()
    {
        TestBlock block = new("clear Command Tests");
        CurBlock = block;
        int pid = CManager.Execute("clear", 1, out bool bg);
        Check(pid > 0 && !bg, "clear command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "clear exits with 0");
        Check(Exec("clear --help") == 0, "clear --help executes");
        return block;
    }

    private static void Check(bool cond, string name)
    {
        if (cond)
        {
            Passed++;
            CurBlock?.Items.Add((true, name));
        }
        else
        {
            Failed++;
            FailedTests.Add(name);
            CurBlock?.Items.Add((false, name));
        }
    }
}