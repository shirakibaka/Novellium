// ComprehensiveCommandTests.cs — Comprehensive shell command test suite
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.Commands;
using Novellium.IO;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.Tests;

public static class ComprehensiveCommandTests
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
        Output.WriteDirectLine("=== COMPREHENSIVE COMMAND & FILESYSTEM TEST SUITE ===", ConsoleColor.Magenta);

        string oldCwd = CManager.CurrentDirectory;
        Output.StartCapture();
        try
        {
            TestPaths();
            TestCdPwd();
            TestTouch();
            TestCat();
            TestMkdir();
            TestRm();
            TestRmdir();
            TestStat();
            TestDf();
            TestUname();
            TestUptime();
            TestFree();
            TestLifecycle();
            TestBg();
            TestDmesg();
        }
        finally
        {
            Output.StopCapture();
            CManager.CurrentDirectory = oldCwd;
        }

        Output.WriteDirectLine();
        if (Failed == 0)
        {
            Output.WriteDirectLine($"COMPREHENSIVE SUITE RESULT: All {Passed} tests passed successfully!", ConsoleColor.Green);
        }
        else
        {
            Output.WriteDirectLine($"COMPREHENSIVE SUITE RESULT: {Passed} passed, {Failed} failed!", ConsoleColor.Red);
            foreach (string f in FailedTests) Output.WriteDirectLine($"  - {f}", ConsoleColor.Red);
        }
        Output.WriteDirectLine();
    }

    private static void TestPaths()
    {
        Output.WriteDirectLine("--- Path Normalization & Resolution Tests ---", ConsoleColor.Yellow);
        Check(CManager.NormalizePath("/") == "/", "NormalizePath root '/'");
        Check(CManager.NormalizePath("/etc") == "/etc", "NormalizePath single dir '/etc'");
        Check(CManager.NormalizePath("///var///") == "/var", "NormalizePath multiple slashes '///var///'");
        Check(CManager.NormalizePath("/etc/../var") == "/var", "NormalizePath parent reference '/etc/../var'");
        Check(CManager.NormalizePath("/../../..") == "/", "NormalizePath root parent clamp '/../../..'");
        Check(CManager.NormalizePath("/a/./b/./c") == "/a/b/c", "NormalizePath dot removal '/a/./b/./c'");
        Check(CManager.NormalizePath("/a/b/c/../../d/./e/../f") == "/a/d/f", "NormalizePath complex relative sequence");

        string old = CManager.CurrentDirectory;
        CManager.CurrentDirectory = "/";
        Check(CManager.ResolvePath("") == "/", "ResolvePath empty returns CWD");
        Check(CManager.ResolvePath("file.txt") == "/file.txt", "ResolvePath relative when CWD is '/'");
        Check(CManager.ResolvePath("/absolute/path") == "/absolute/path", "ResolvePath absolute remains absolute");

        CManager.CurrentDirectory = "/home/user";
        Check(CManager.ResolvePath("docs/readme.txt") == "/home/user/docs/readme.txt", "ResolvePath nested relative");
        Check(CManager.ResolvePath("../etc") == "/home/etc", "ResolvePath relative with parent '..'");
        CManager.CurrentDirectory = old;
    }

    private static void TestCdPwd()
    {
        Output.WriteDirectLine("--- cd & pwd Tests ---", ConsoleColor.Yellow);
        Check(Exec("cd") == 0 && CManager.CurrentDirectory == "/", "cd with no arguments resets CWD to '/'");
        Check(Exec("pwd") == 0, "pwd in '/' exits 0");
        Check(Exec("cd /etc") == 0 && CManager.CurrentDirectory == "/etc", "cd /etc sets CWD to '/etc'");
        Check(Exec("cd .") == 0 && CManager.CurrentDirectory == "/etc", "cd . maintains CWD");
        Check(Exec("cd ..") == 0 && CManager.CurrentDirectory == "/", "cd .. moves to parent directory '/'");
        Check(Exec("cd ..") == 0 && CManager.CurrentDirectory == "/", "cd .. at '/' stays at '/'");
        Check(Exec("cd tmp") == 0 && CManager.CurrentDirectory == "/tmp", "cd relative path from '/' to 'tmp'");

        string before = CManager.CurrentDirectory;
        Exec("cd /nonexistent_test_directory_xyz");
        Check(CManager.CurrentDirectory == before, "cd to nonexistent directory preserves CWD");

        before = CManager.CurrentDirectory;
        Exec("cd /etc/hostname");
        Check(CManager.CurrentDirectory == before, "cd to regular file preserves CWD");

        Check(Exec("cd --help") == 0, "cd --help exits with 0");
        CManager.CurrentDirectory = "/";
    }

    private static void TestTouch()
    {
        Output.WriteDirectLine("--- touch Tests ---", ConsoleColor.Yellow);
        Check(Exec("touch") == 0, "touch without operands handles gracefully");

        string target = "/tmp/test_touch_single.txt";
        VfsManager.TryUnlink(target);
        Check(Exec($"touch {target}") == 0 && VfsManager.TryStat(target, out _), "touch creates single new file");
        Check(Exec($"touch {target}") == 0, "touch existing file succeeds without error");

        string f1 = "/tmp/test_multi_1.txt", f2 = "/tmp/test_multi_2.txt";
        VfsManager.TryUnlink(f1); VfsManager.TryUnlink(f2);
        Check(Exec($"touch {f1} {f2}") == 0 && VfsManager.TryStat(f1, out _) && VfsManager.TryStat(f2, out _), "touch creates multiple files in single invocation");

        CManager.CurrentDirectory = "/tmp";
        string rel = "test_rel_touch.txt";
        VfsManager.TryUnlink("/tmp/" + rel);
        Check(Exec($"touch {rel}") == 0 && VfsManager.TryStat("/tmp/" + rel, out _), "touch with relative path resolves to current directory");

        Check(Exec("touch --invalid-option") == 0, "touch with invalid option exits cleanly");

        VfsManager.TryUnlink(target);
        VfsManager.TryUnlink(f1);
        VfsManager.TryUnlink(f2);
        VfsManager.TryUnlink("/tmp/" + rel);
        CManager.CurrentDirectory = "/";
    }

    private static void TestCat()
    {
        Output.WriteDirectLine("--- cat Tests ---", ConsoleColor.Yellow);
        Check(Exec("cat /etc/hostname") == 0, "cat /etc/hostname succeeds");
        Check(Exec("cat /etc/hostname /etc/motd") == 0, "cat multiple files succeeds");
        Check(Exec("cat -n /etc/os-release") == 0, "cat -n succeeds");

        CManager.CurrentDirectory = "/etc";
        Check(Exec("cat hostname") == 0, "cat relative path succeeds");
        CManager.CurrentDirectory = "/";

        Check(Exec("cat /nonexistent_file_12345.txt") == 0, "cat nonexistent file handles gracefully without crash");
        Check(Exec("cat /etc") == 0, "cat on directory detects and handles gracefully");
        Check(Exec("cat") == 0, "cat with no args prints usage and exits cleanly");
        Check(Exec("cat --help") == 0, "cat --help exits 0");
    }

    private static void TestMkdir()
    {
        Output.WriteDirectLine("--- mkdir Tests ---", ConsoleColor.Yellow);
        Check(Exec("mkdir") == 0, "mkdir without operands handles gracefully");

        string dir1 = "/tmp/test_mkdir_single";
        VfsManager.TryRemoveDirectory(dir1);
        Check(Exec($"mkdir {dir1}") == 0 && VfsManager.TryStat(dir1, out VfsStat st) && st.IsDirectory, "mkdir creates single directory");
        Check(Exec($"mkdir {dir1}") == 0, "mkdir on existing directory handles duplicate gracefully");
        Check(Exec($"mkdir -p {dir1}") == 0, "mkdir -p on existing directory succeeds");

        string deep = "/tmp/test_deep/level1/level2/level3";
        Check(Exec($"mkdir -p {deep}") == 0 && VfsManager.TryStat(deep, out VfsStat dst) && dst.IsDirectory, "mkdir -p creates nested directory hierarchy");

        CManager.CurrentDirectory = "/tmp";
        string rel = "test_mkdir_relative";
        VfsManager.TryRemoveDirectory("/tmp/" + rel);
        Check(Exec($"mkdir {rel}") == 0 && VfsManager.TryStat("/tmp/" + rel, out VfsStat rst) && rst.IsDirectory, "mkdir relative directory resolves to CWD");

        VfsManager.TryRemoveDirectory("/tmp/" + rel);
        VfsManager.TryRemoveDirectory(deep);
        VfsManager.TryRemoveDirectory("/tmp/test_deep/level1/level2");
        VfsManager.TryRemoveDirectory("/tmp/test_deep/level1");
        VfsManager.TryRemoveDirectory("/tmp/test_deep");
        VfsManager.TryRemoveDirectory(dir1);
        CManager.CurrentDirectory = "/";
    }

    private static void TestRm()
    {
        Output.WriteDirectLine("--- rm Tests ---", ConsoleColor.Yellow);
        Check(Exec("rm") == 0, "rm without operands handles gracefully");

        string target = "/tmp/test_rm_single.txt";
        VfsManager.TryCreateFile(target, (VfsMode)420);
        Check(VfsManager.TryStat(target, out _), "file created before rm");
        Check(Exec($"rm {target}") == 0 && !VfsManager.TryStat(target, out _), "rm removes regular file");

        Check(Exec("rm /tmp/nonexistent_rm_target_999.txt") == 0, "rm on nonexistent file reports error without crashing");
        Check(Exec("rm -f /tmp/nonexistent_rm_target_999.txt") == 0, "rm -f ignores nonexistent file");

        string dirTarget = "/tmp/test_rm_dir_guard";
        VfsManager.TryCreateDirectory(dirTarget, (VfsMode)493);
        Exec($"rm {dirTarget}");
        Check(VfsManager.TryStat(dirTarget, out VfsStat dStat) && dStat.IsDirectory, "rm refuses to remove directory");
        VfsManager.TryRemoveDirectory(dirTarget);

        string f1 = "/tmp/test_rm_multi1.txt", f2 = "/tmp/test_rm_multi2.txt";
        VfsManager.TryCreateFile(f1, (VfsMode)420);
        VfsManager.TryCreateFile(f2, (VfsMode)420);
        Check(Exec($"rm -f {f1} {f2}") == 0 && !VfsManager.TryStat(f1, out _) && !VfsManager.TryStat(f2, out _), "rm -f removes multiple files");
    }

    private static void TestRmdir()
    {
        Output.WriteDirectLine("--- rmdir Tests ---", ConsoleColor.Yellow);
        Check(Exec("rmdir") == 0, "rmdir without operands handles gracefully");

        string empty = "/tmp/test_rmdir_empty";
        VfsManager.TryCreateDirectory(empty, (VfsMode)493);
        Check(VfsManager.TryStat(empty, out _), "empty directory created before rmdir");
        Check(Exec($"rmdir {empty}") == 0 && !VfsManager.TryStat(empty, out _), "rmdir removes empty directory");

        string nonEmpty = "/tmp/test_rmdir_nonempty", child = nonEmpty + "/item.txt";
        VfsManager.TryCreateDirectory(nonEmpty, (VfsMode)493);
        VfsManager.TryCreateFile(child, (VfsMode)420);
        Check(Exec($"rmdir {nonEmpty}") == 0 && VfsManager.TryStat(nonEmpty, out _), "rmdir refuses to remove non-empty directory");

        VfsManager.TryUnlink(child);
        Check(Exec($"rmdir {nonEmpty}") == 0 && !VfsManager.TryStat(nonEmpty, out _), "rmdir succeeds after directory emptied");

        string reg = "/tmp/test_rmdir_file.txt";
        VfsManager.TryCreateFile(reg, (VfsMode)420);
        Check(Exec($"rmdir {reg}") == 0 && VfsManager.TryStat(reg, out _), "rmdir refuses to remove regular file");
        VfsManager.TryUnlink(reg);

        Check(Exec("rmdir /tmp/nonexistent_dir_999") == 0, "rmdir nonexistent directory handles gracefully");
    }

    private static void TestStat()
    {
        Output.WriteDirectLine("--- stat Tests ---", ConsoleColor.Yellow);
        Check(Exec("stat") == 0, "stat without operands handles gracefully");
        Check(Exec("stat /etc/hostname") == 0, "stat on regular file /etc/hostname succeeds");
        Check(Exec("stat /etc") == 0, "stat on directory /etc succeeds");

        CManager.CurrentDirectory = "/etc";
        Check(Exec("stat hostname") == 0, "stat relative path 'hostname' succeeds");
        CManager.CurrentDirectory = "/";

        Check(Exec("stat /nonexistent_stat_xyz") == 0, "stat on nonexistent path handles gracefully");
        Check(Exec("stat --help") == 0, "stat --help exits 0");
    }

    private static void TestDf()
    {
        Output.WriteDirectLine("--- df Tests ---", ConsoleColor.Yellow);
        Check(Exec("df") == 0, "df default executes and exits 0");
        Check(Exec("df -h") == 0, "df -h executes and exits 0");
        Check(Exec("df -k") == 0, "df -k executes and exits 0");
        Check(Exec("df -m") == 0, "df -m executes and exits 0");
        Check(Exec("df /") == 0, "df / executes and exits 0");
        Check(Exec("df -h /") == 0, "df -h / executes and exits 0");
        Check(Exec("df --help") == 0, "df --help exits 0");
    }

    private static void TestUname()
    {
        Output.WriteDirectLine("--- uname Tests ---", ConsoleColor.Yellow);
        string[] flags = ["", "-s", "-n", "-r", "-v", "-m", "-a", "-srm", "-snrvm"];
        bool all = true;
        foreach (string flag in flags)
        {
            string cmd = string.IsNullOrEmpty(flag) ? "uname" : $"uname {flag}";
            if (Exec(cmd) != 0) { all = false; break; }
        }
        Check(all, "uname supports all individual and combined flags");
        Check(Exec("uname -z") == 0, "uname handles invalid flag gracefully");
        Check(Exec("uname --help") == 0, "uname --help exits 0");
    }

    private static void TestUptime()
    {
        Output.WriteDirectLine("--- uptime Tests ---", ConsoleColor.Yellow);
        Check(Exec("uptime") == 0, "uptime executes and exits 0");
        Check(Exec("uptime -p") == 0, "uptime -p executes and exits 0");
        Check(Exec("uptime -s") == 0, "uptime -s executes and exits 0");
        Check(Exec("uptime --unknown") == 0, "uptime handles invalid option gracefully");
        Check(Exec("uptime --help") == 0, "uptime --help exits 0");
    }

    private static void TestFree()
    {
        Output.WriteDirectLine("--- free Tests ---", ConsoleColor.Yellow);
        string[] cmds = ["free", "free -b", "free -k", "free -m", "free -h", "free --human", "free --mega"];
        bool all = true;
        foreach (string cmd in cmds)
        {
            if (Exec(cmd) != 0) { all = false; break; }
        }
        Check(all, "free supports -b, -k, -m, -h, --human, --mega");
        Check(Exec("free -x") == 0, "free handles invalid option gracefully");
        Check(Exec("free --help") == 0, "free --help exits 0");
    }

    private static void TestLifecycle()
    {
        Output.WriteDirectLine("--- End-To-End Developer Workflow Lifecycle ---", ConsoleColor.Yellow);
        CManager.CurrentDirectory = "/";
        Check(CManager.CurrentDirectory == "/", "Workflow starts at '/'");

        string ws = "/tmp/workspace_e2e";
        Exec($"mkdir -p {ws}/src {ws}/docs");
        Check(VfsManager.TryStat($"{ws}/src", out _) && VfsManager.TryStat($"{ws}/docs", out _), "e2e: created workspace src and docs directories");

        Exec($"cd {ws}");
        Check(CManager.CurrentDirectory == ws, "e2e: cd into workspace directory");

        Exec("touch Makefile README.txt");
        Check(VfsManager.TryStat($"{ws}/Makefile", out _) && VfsManager.TryStat($"{ws}/README.txt", out _), "e2e: relative touch created Makefile and README.txt");

        Exec("cd src");
        Check(CManager.CurrentDirectory == $"{ws}/src", "e2e: cd src resolves to nested path");

        Exec("touch main.c");
        Check(VfsManager.TryStat($"{ws}/src/main.c", out _), "e2e: touch main.c in nested directory");
        Check(Exec("stat main.c") == 0, "e2e: stat main.c succeeds");
        Check(Exec("ls -la") == 0, "e2e: ls -la succeeds");

        Exec("cd ..");
        Check(CManager.CurrentDirectory == ws, "e2e: cd .. returns to workspace root");

        Exec("rmdir src");
        Check(VfsManager.TryStat($"{ws}/src", out _), "e2e: rmdir src fails while files remain");

        Exec("rm -f Makefile README.txt src/main.c");
        Check(!VfsManager.TryStat($"{ws}/src/main.c", out _), "e2e: rm -f deletes nested main.c");

        Exec("rmdir src docs");
        Check(!VfsManager.TryStat($"{ws}/src", out _) && !VfsManager.TryStat($"{ws}/docs", out _), "e2e: rmdir src and docs succeeds");

        Exec("cd /");
        Exec($"rmdir {ws}");
        Check(!VfsManager.TryStat(ws, out _), "e2e: workspace fully removed and cleaned up");
    }

    private static void TestBg()
    {
        Output.WriteDirectLine("--- Background Execution of Utilities (&) ---", ConsoleColor.Yellow);
        string bgFile = "/tmp/test_bg_touch.txt";
        VfsManager.TryUnlink(bgFile);

        int pid = CManager.Execute($"touch {bgFile} &", 1, out bool bg);
        Check(bg && pid > 0, "touch executed in background with '&'");

        bool waited = PManager.Wait(1, pid, out int code);
        Check(waited && code == 0, "background touch finishes and reaps with exit 0");
        Check(VfsManager.TryStat(bgFile, out _), "background touch actually created file");

        pid = CManager.Execute($"rm -f {bgFile} &", 1, out bg);
        Check(bg && pid > 0, "rm executed in background with '&'");

        waited = PManager.Wait(1, pid, out code);
        Check(waited && code == 0, "background rm finishes and reaps with exit 0");
        Check(!VfsManager.TryStat(bgFile, out _), "background rm actually deleted file");
    }

    private static void TestDmesg()
    {
        Output.WriteDirectLine("--- dmesg & syslog Integration Tests ---", ConsoleColor.Yellow);
        Syslogd.Info("test_suite", "Comprehensive test suite entry 1");
        Syslogd.Warn("test_suite", "Comprehensive test suite entry 2");
        Syslogd.Error("test_suite", "Comprehensive test suite entry 3");

        var logs = Syslogd.GetRecentLogs();
        Check(logs.Count >= 3, "Syslog captured all test messages");
        Check(Exec("dmesg") == 0, "dmesg displays logged system messages");
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
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
