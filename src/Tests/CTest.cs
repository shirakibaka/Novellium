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
        Output.WriteLine("=== COMMAND TESTS ===", ConsoleColor.Cyan);

        TestUnknownCommand();
        TestForeground();
        TestBackground();
        TestLsCommand();
        TestFilesystemCommands();
        TestSystemInfoCommands();
        TestHelpCommand();
        TestCommandHelpFlags();
        TestDmesgCommand();
        TestSyslogd();
        TestClearCommand();
        ComprehensiveCommandTests.Run();

        Output.WriteLine();
        if (Failed == 0)
        {
            Output.WriteLine($"RESULT: {Passed} passed", ConsoleColor.Green);
        }
        else
        {
            Output.WriteLine($"RESULT: {Passed} passed, {Failed} failed", ConsoleColor.Red);
            Output.WriteLine("Failed tests:", ConsoleColor.Red);
            foreach (string fail in FailedTests) Output.WriteLine($"  - {fail}", ConsoleColor.Red);
        }
        Output.WriteLine();
    }

    private static void TestUnknownCommand()
    {
        int pid = CManager.Execute("doesnotexist", 1, out bool bg);
        Check(pid == 0, "unknown command");
        Check(!bg, "unknown command is not background");
    }

    private static void TestForeground()
    {
        int pid = CManager.Execute("sleep 0", 1, out bool bg);
        Check(pid > 0, "foreground command returns PID");
        Check(!bg, "foreground command detected");

        bool waited = PManager.Wait(1, pid, out int exitCode);
        Check(waited, "foreground command waits");
        Check(exitCode == 0, "foreground command exits with code 0");
        Check(PManager.Get(pid) == null, "foreground process is reaped");
    }

    private static void TestBackground()
    {
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
    }

    private static void TestLsCommand()
    {
        int pid = CManager.Execute("ls", 1, out bool bg);
        Check(pid > 0 && !bg, "ls command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "ls exits with 0");

        Check(Exec("ls -l /etc") == 0, "ls -l /etc executes");
        Check(Exec("ls -a /bin") == 0, "ls -a /bin executes");
        Check(Exec("ls -1 /tmp") == 0, "ls -1 /tmp executes");
        Check(Exec("ls --help") == 0, "ls --help executes");
        Check(Exec("ls -z") >= 0, "ls -z executes");
    }

    private static void TestFilesystemCommands()
    {
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
    }

    private static void TestSystemInfoCommands()
    {
        Check(Exec("uname -a") == 0, "uname command executes");
        Check(Exec("uptime") == 0, "uptime command executes");
        Check(Exec("free -m") == 0, "free command executes");
    }

    private static void TestHelpCommand()
    {
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
    }

    private static void TestCommandHelpFlags()
    {
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
    }

    private static void TestDmesgCommand()
    {
        int pid = CManager.Execute("dmesg", 1, out bool bg);
        Check(pid > 0 && !bg, "dmesg command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "dmesg exits with 0");
        Check(Exec("dmesg -h") == 0, "dmesg -h executes");
    }

    private static void TestSyslogd()
    {
        Syslogd.Info("test", "test syslogd log entry");
        var recent = Syslogd.GetRecentLogs();
        Check(recent.Count > 0, "syslogd buffer captures log entry");

        int pid = Syslogd.Start();
        Check(pid > 0, "syslogd daemon started");
    }

    private static void TestClearCommand()
    {
        int pid = CManager.Execute("clear", 1, out bool bg);
        Check(pid > 0 && !bg, "clear command executes");
        PManager.Wait(1, pid, out int code);
        Check(code == 0, "clear exits with 0");
        Check(Exec("clear --help") == 0, "clear --help executes");
    }

    private static void Check(bool cond, string name)
    {
        if (cond)
        {
            Passed++;
            OutputInfo.Test(true, name);
        }
        else
        {
            Failed++;
            FailedTests.Add(name);
            OutputInfo.Test(false, name);
        }
    }
}