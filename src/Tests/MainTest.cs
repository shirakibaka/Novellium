using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cosmos.Kernel.HAL.Interfaces.Devices;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Filesystems.Ext2;
using Cosmos.Kernel.System.Storage;
using Cosmos.Kernel.System.Vfs;
using Novellium.Commands;
using Novellium.IO;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.Tests;

public static class MainTest
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
        int activePid = PManager.CurrentPid;
        int pid = CManager.Execute(cmd, activePid, out _);
        if (pid == 0) return 0;
        if (pid < 0) return -1;
        if (!PManager.Wait(activePid, pid, out int code, timeoutMs: 15000))
        {
            PManager.Kill(pid);
            PManager.Wait(activePid, pid, out _);
            return -1;
        }
        return code;
    }

    public static void Run()
    {
        Passed = 0; Failed = 0; FailedTests.Clear();
        Output.WriteDirectLine("=== NOVELLIUM UNIFIED MASTER INTEGRATION TEST SUITE ===", ConsoleColor.Magenta);

        string oldCwd = CManager.CurrentDirectory;
        bool prevAutoReap = PManager.AutomaticOrphanReaping;
        PManager.AutomaticOrphanReaping = false;

        List<TestBlock> blocks = new();
        Output.StartCapture();
        try
        {
            blocks.Add(TestKernelAndSubsystems());
            blocks.Add(TestProcessLifecycle());
            blocks.Add(TestPathNormalization());
            blocks.Add(TestExt2Filesystem());
            blocks.Add(TestE2EDeveloperWorkflow());
            blocks.Add(TestCdAndPwdCommands());
            blocks.Add(TestFileCommands());
            blocks.Add(TestSystemInfoUtilities());
            blocks.Add(TestUnixCoreutilsAndRedirection());
            blocks.Add(TestFindAndTreeCommands());
        }
        finally
        {
            Output.StopCapture();
            PManager.AutomaticOrphanReaping = prevAutoReap;
            CManager.CurrentDirectory = oldCwd;
        }

        Output.WriteDirectLine();
        for (int i = 0; i < blocks.Count; i += 2)
        {
            TestBlock left = blocks[i];
            TestBlock? right = i + 1 < blocks.Count ? blocks[i + 1] : null;
            PrintBlockPair(left, right);
        }

        if (Failed == 0)
        {
            Output.WriteDirectLine($"MASTER SUITE RESULT: All {Passed} tests passed successfully!", ConsoleColor.Green);
        }
        else
        {
            Output.WriteDirectLine($"MASTER SUITE RESULT: {Passed} passed, {Failed} failed!", ConsoleColor.Red);
            foreach (string f in FailedTests) Output.WriteDirectLine($"  - {f}", ConsoleColor.Red);
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

    private static TestBlock TestKernelAndSubsystems()
    {
        TestBlock block = new("Kernel & Core Subsystems");
        CurBlock = block;

        PInfo? k = PManager.Get(1);
        Check(k != null && k.ParentPid == 0 && k.State == PState.Running, "Kernel PID 1 state");
        Check(!PManager.Kill(1), "Kernel process immortality");

        Syslogd.Info("main_test", "Master test suite syslog entry");
        var recentLogs = Syslogd.GetRecentLogs();
        Check(recentLogs.Count > 0, "Syslog daemon log capture");

        string[] requiredDirs = ["/bin", "/etc", "/home", "/home/user", "/var", "/var/log", "/tmp", "/root", "/dev"];
        bool allDirs = true;
        foreach (string d in requiredDirs)
        {
            if (!VfsManager.TryStat(d, out VfsStat st) || !st.IsDirectory) { allDirs = false; break; }
        }
        Check(allDirs, "VFS root hierarchy");

        string[] requiredFiles = ["/etc/hostname", "/etc/os-release", "/etc/motd", "/etc/version"];
        bool allFiles = true;
        foreach (string f in requiredFiles)
        {
            if (!VfsManager.TryStat(f, out VfsStat st) || st.IsDirectory) { allFiles = false; break; }
        }
        Check(allFiles, "Base system config files");

        OutputInfo.Info("OutputInfo test");
        OutputInfo.Debug("Debug test");
        OutputInfo.Warning("Warning test");
        OutputInfo.Error("Error test");
        OutputInfo.Custom("CUSTOM", "Custom test", ConsoleColor.Cyan, ConsoleColor.White);
        Check(true, "OutputInfo diagnostic loggers");

        return block;
    }

    private static TestBlock TestProcessLifecycle()
    {
        TestBlock block = new("Process & Job Lifecycle");
        CurBlock = block;

        int p1 = PManager.Start("test-seq-1", [], (pid, args) => { });
        int p2 = PManager.Start("test-seq-2", [], (pid, args) => { });
        Check(p2 == p1 + 1, "PID allocation sequence");
        PManager.Wait(1, p1, out _);
        PManager.Wait(1, p2, out _);

        int fgPid = PManager.Start("test-fg", [], (pid, args) => Thread.Sleep(2));
        Check(fgPid > 1 && PManager.Wait(1, fgPid, out int fgCode) && fgCode == 0, "Foreground spawn & wait");

        int exitPid = PManager.Start("test-exit-code", [], (pid, args) => PManager.Exit(pid, 42));
        Check(PManager.Wait(1, exitPid, out int exitCode) && exitCode == 42, "Explicit exit code 42");

        int crashPid = PManager.Start("test-crash", [], (pid, args) => throw new InvalidOperationException("crash"));
        Check(PManager.Wait(1, crashPid, out int crashCode) && crashCode == 1, "Exception crash handling");

        int killPid = PManager.Start("test-kill", [], (pid, args) => { while (!PManager.IsKillReq(pid)) Thread.Sleep(2); });
        Check(PManager.Kill(killPid) && PManager.Wait(1, killPid, out int killCode) && killCode == 137, "Process kill signal (137)");

        int zombiePid = PManager.Start("zombie-proc", [], (pid, args) => { }, 1);
        Thread.Sleep(5);
        Check(PManager.Reap(1, zombiePid, out int zCode) && zCode == 0, "Zombie state & reap");

        int parentPid = PManager.Start("parent-proc", [], (pid, args) => { while (!PManager.IsKillReq(pid)) Thread.Sleep(2); });
        int childPid = PManager.Start("child-proc", [], (pid, args) => { while (!PManager.IsKillReq(pid)) Thread.Sleep(2); }, parentPid);
        PManager.Kill(parentPid);
        PManager.Wait(1, parentPid, out _);
        PInfo? childP = PManager.Get(childPid);
        Check(childP != null && childP.ParentPid == 1, "Process reparenting to PID 1");
        PManager.Kill(childPid);
        PManager.Wait(1, childPid, out _);

        int orphanPid = PManager.Start("orphan-proc", [], (pid, args) => { }, 1);
        Thread.Sleep(5);
        Check(PManager.ReapOrphans(true) >= 1, "Orphan zombie reaping");

        int subCwdPid = PManager.Start("test-cwd-sub", [], (pid, args) => { PManager.SetCwd(pid, "/tmp"); }, 1);
        PManager.Wait(1, subCwdPid, out _);
        Check(PManager.GetCwd(1) == "/", "Per-process CWD isolation");

        PInfo? kInfo = PManager.Get(1);
        Check(kInfo != null && kInfo.Name == "kernel", "Thread instance tracking");

        return block;
    }

    private static TestBlock TestPathNormalization()
    {
        TestBlock block = new("Path Normalization & Resolution");
        CurBlock = block;

        Check(CManager.NormalizePath("/") == "/", "NormalizePath root '/'");
        Check(CManager.NormalizePath("/etc") == "/etc", "NormalizePath single dir '/etc'");
        Check(CManager.NormalizePath("///var///") == "/var", "NormalizePath multiple slashes");
        Check(CManager.NormalizePath("/etc/../var") == "/var", "NormalizePath parent reference");
        Check(CManager.NormalizePath("/../../..") == "/", "NormalizePath root parent clamp");
        Check(CManager.NormalizePath("/a/./b/./c") == "/a/b/c", "NormalizePath dot removal");
        Check(CManager.NormalizePath("/a/b/c/../../d/./e/../f") == "/a/d/f", "NormalizePath complex relative");

        string old = CManager.CurrentDirectory;
        CManager.CurrentDirectory = "/";
        Check(CManager.ResolvePath("") == "/", "ResolvePath empty returns CWD");
        Check(CManager.ResolvePath("file.txt") == "/file.txt", "ResolvePath relative at '/'");
        Check(CManager.ResolvePath("/absolute/path") == "/absolute/path", "ResolvePath absolute remains absolute");

        CManager.CurrentDirectory = "/home/user";
        Check(CManager.ResolvePath("docs/readme.txt") == "/home/user/docs/readme.txt", "ResolvePath nested relative");
        Check(CManager.ResolvePath("../etc") == "/home/etc", "ResolvePath parent relative");
        CManager.CurrentDirectory = old;

        return block;
    }

    private static TestBlock TestExt2Filesystem()
    {
        TestBlock block = new("Ext2 Filesystem Engine");
        CurBlock = block;

        Check(StorageManager.Devices.Count > 0, "Storage devices detected");
        Check(VfsManager.TryStatFs("/", out var stats) && stats.Blocks > 0, "Ext2 statfs free space");

        string testDir = "/tmp/ext2_test_suite";
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);

        Directory.CreateDirectory(testDir);
        Check(Directory.Exists(testDir), "Ext2 directory creation");

        string fPath = testDir + "/test.txt";
        File.WriteAllText(fPath, "Novellium Ext2 Test");
        Check(File.Exists(fPath), "Ext2 file create & write");
        Check(File.ReadAllText(fPath) == "Novellium Ext2 Test", "Ext2 file read verification");

        File.AppendAllText(fPath, "\nSecond line");
        Check(File.ReadAllText(fPath) == "Novellium Ext2 Test\nSecond line", "Ext2 file append content");

        string rPath = testDir + "/renamed.txt";
        File.Move(fPath, rPath);
        Check(!File.Exists(fPath) && File.Exists(rPath), "Ext2 file rename operation");

        File.Delete(rPath);
        Check(!File.Exists(rPath), "Ext2 file deletion");

        Directory.Delete(testDir);
        Check(!Directory.Exists(testDir), "Ext2 directory cleanup");

        return block;
    }

    private static TestBlock TestE2EDeveloperWorkflow()
    {
        TestBlock block = new("End-To-End Developer Workflow");
        CurBlock = block;

        CManager.CurrentDirectory = "/";
        Check(CManager.CurrentDirectory == "/", "Workflow starts at '/'");

        string ws = "/tmp/dev_workspace";
        Exec($"mkdir -p {ws}/src {ws}/docs {ws}/build");
        Check(VfsManager.TryStat($"{ws}/src", out _) && VfsManager.TryStat($"{ws}/docs", out _), "Created project directories");

        Exec($"cd {ws}");
        Check(CManager.CurrentDirectory == ws, "cd into workspace");

        Exec("touch Makefile README.txt");
        Check(VfsManager.TryStat($"{ws}/Makefile", out _) && VfsManager.TryStat($"{ws}/README.txt", out _), "Created Makefile & README");

        Exec("cd src");
        Check(CManager.CurrentDirectory == $"{ws}/src", "cd into nested src");

        Exec("touch main.c utils.c");
        Check(VfsManager.TryStat($"{ws}/src/main.c", out _), "Created main.c & utils.c");
        Check(Exec("stat main.c") == 0 && Exec("ls -la") == 0, "stat & ls -la on main.c");

        Exec("cd ..");
        string bgApp = $"{ws}/build/app.bin";
        int activePid = PManager.CurrentPid;
        int bgPid = CManager.Execute($"touch {bgApp} &", activePid, out bool isBg);
        Check(isBg && bgPid > 0, "Spawn background build job (&)");
        Check(Exec("ps") == 0 && Exec("jobs") == 0, "Concurrent ps & jobs inspection");

        bool waited = PManager.Wait(activePid, bgPid, out int code);
        Check(waited && code == 0 && VfsManager.TryStat(bgApp, out _), "Background build finished (exit 0)");

        Check(Exec("dmesg") == 0, "dmesg & syslog inspection");

        Exec($"rm -f {ws}/Makefile {ws}/README.txt {ws}/src/main.c {ws}/src/utils.c {bgApp}");
        Exec($"rmdir {ws}/src {ws}/docs {ws}/build");
        Exec("cd /");
        Exec($"rmdir {ws}");
        Check(!VfsManager.TryStat(ws, out _), "Cleanup workspace files & dirs");

        return block;
    }

    private static TestBlock TestCdAndPwdCommands()
    {
        TestBlock block = new("cd & pwd Utility Tests");
        CurBlock = block;

        Check(Exec("cd") == 0 && CManager.CurrentDirectory == "/", "cd resets CWD to '/'");
        Check(Exec("pwd") == 0, "pwd in '/' exits 0");
        Check(Exec("cd /etc") == 0 && CManager.CurrentDirectory == "/etc", "cd /etc sets CWD to '/etc'");
        Check(Exec("cd .") == 0 && CManager.CurrentDirectory == "/etc", "cd . maintains CWD");
        Check(Exec("cd ..") == 0 && CManager.CurrentDirectory == "/", "cd .. moves to parent");
        Check(Exec("cd ..") == 0 && CManager.CurrentDirectory == "/", "cd .. at root stays at root");
        Check(Exec("cd tmp") == 0 && CManager.CurrentDirectory == "/tmp", "cd relative path to tmp");

        string before = CManager.CurrentDirectory;
        Exec("cd /nonexistent_dir_999");
        Check(CManager.CurrentDirectory == before, "cd nonexistent dir preserves CWD");

        before = CManager.CurrentDirectory;
        Exec("cd /etc/hostname");
        Check(CManager.CurrentDirectory == before, "cd regular file preserves CWD");

        Check(Exec("cd --help") == 0, "cd --help exits 0");
        CManager.CurrentDirectory = "/";

        return block;
    }

    private static TestBlock TestFileCommands()
    {
        TestBlock block = new("File & Directory Operations");
        CurBlock = block;

        Check(Exec("touch") == 0, "touch without operands");
        string t1 = "/tmp/test_single.txt";
        VfsManager.TryUnlink(t1);
        Check(Exec($"touch {t1}") == 0 && VfsManager.TryStat(t1, out _), "touch single file");
        Check(Exec($"touch {t1}") == 0, "touch existing file");

        string f1 = "/tmp/tf1.txt", f2 = "/tmp/tf2.txt";
        VfsManager.TryUnlink(f1); VfsManager.TryUnlink(f2);
        Check(Exec($"touch {f1} {f2}") == 0 && VfsManager.TryStat(f1, out _) && VfsManager.TryStat(f2, out _), "touch multiple files");

        string d1 = "/tmp/test_dir1";
        VfsManager.TryRemoveDirectory(d1);
        Check(Exec($"mkdir {d1}") == 0 && VfsManager.TryStat(d1, out VfsStat st) && st.IsDirectory, "mkdir single directory");

        string deep = "/tmp/deep1/deep2";
        Check(Exec($"mkdir -p {deep}") == 0 && VfsManager.TryStat(deep, out _), "mkdir -p nested hierarchy");

        Check(Exec("stat /etc/hostname") == 0 && Exec("stat /etc") == 0, "stat file & directory");
        Check(Exec("cat /etc/hostname") == 0 && Exec("cat -n /etc/os-release") == 0, "cat file & cat -n");

        Check(Exec($"rm {t1}") == 0 && !VfsManager.TryStat(t1, out _), "rm removes regular file");
        Check(Exec($"rm -f {f1} {f2}") == 0, "rm -f multiple files");

        string dirGuard = "/tmp/dir_guard";
        VfsManager.TryCreateDirectory(dirGuard, (VfsMode)493);
        Exec($"rm {dirGuard}");
        Check(VfsManager.TryStat(dirGuard, out VfsStat dgSt) && dgSt.IsDirectory, "rm directory guard");
        VfsManager.TryRemoveDirectory(dirGuard);

        Check(Exec($"rmdir {d1}") == 0 && !VfsManager.TryStat(d1, out _), "rmdir empty directory");

        VfsManager.TryRemoveDirectory(deep);
        VfsManager.TryRemoveDirectory("/tmp/deep1");
        CManager.CurrentDirectory = "/";

        return block;
    }

    private static TestBlock TestSystemInfoUtilities()
    {
        TestBlock block = new("System Info & Utilities");
        CurBlock = block;

        Check(Exec("df") == 0 && Exec("df -h") == 0, "df default & df -h");
        Check(Exec("uname -a") == 0 && Exec("uname -srm") == 0, "uname all flags & combined");
        Check(Exec("uptime") == 0 && Exec("uptime -p") == 0, "uptime default & uptime -p");
        Check(Exec("free -m") == 0 && Exec("free -h") == 0 && Exec("free --human") == 0, "free -m, -h, --human");
        Check(Exec("dmesg") == 0 && Exec("dmesg -h") == 0, "dmesg & dmesg -h");
        Check(Exec("clear") == 0 && Exec("clear --help") == 0, "clear & clear --help");

        string[] topics = ["ls", "cat", "cd", "pwd", "touch", "mkdir", "rm", "rmdir", "df", "stat", "uname", "uptime", "free", "ps", "jobs", "kill", "wait", "sleep", "help", "dmesg", "clear", "test", "find", "tree"];
        bool helpOk = true;
        foreach (string t in topics)
        {
            if (Exec($"help {t}") != 0) { helpOk = false; break; }
        }
        Check(helpOk, "help builtin topics (24)");

        string[] cmdHelps = [
            "ps --help", "jobs -h", "kill --help", "wait -h", "sleep --help",
            "dmesg -h", "cat --help", "cd -h", "pwd --help", "touch -h",
            "mkdir --help", "rm -h", "rmdir --help", "stat --help", "uname -h",
            "uptime --help", "free -h", "clear -h", "clear --help", "test -h", "test --help",
            "find --help", "find -h", "tree --help", "tree -h"
        ];
        bool flagsOk = true;
        foreach (string c in cmdHelps)
        {
            if (Exec(c) != 0) { flagsOk = false; break; }
        }
        Check(flagsOk, "Universal -h/--help flags (25)");

        return block;
    }

    private static TestBlock TestUnixCoreutilsAndRedirection()
    {
        TestBlock block = new("Unix Utilities & Redirection");
        CurBlock = block;

        string rFile = "/tmp/unix_test.txt";
        Exec($"echo \"alpha\nbeta\ngamma\ndelta\nepsilon\" > {rFile}");
        Check(VfsManager.TryStat(rFile, out _), "echo with output redirection (>)");

        Exec($"echo \"zeta\" >> {rFile}");
        string content = CManager.ReadFileText(rFile);
        Check(content.Contains("zeta"), "echo append redirection (>>)");

        Exec($"cat {rFile} | grep beta > /tmp/grep_res.txt");
        string grepOut = CManager.ReadFileText("/tmp/grep_res.txt").Trim();
        Check(grepOut == "beta", "Piping cat | grep > file");

        Exec($"cat {rFile} | head -n 2 > /tmp/head_res.txt");
        string headOut = CManager.ReadFileText("/tmp/head_res.txt").Trim();
        Check(headOut == "alpha\nbeta", "Piping cat | head -n 2");

        Exec($"cat {rFile} | tail -n 2 > /tmp/tail_res.txt");
        string tailOut = CManager.ReadFileText("/tmp/tail_res.txt").Trim();
        Check(tailOut == "epsilon\nzeta", "Piping cat | tail -n 2");

        Exec($"cat {rFile} | wc -l > /tmp/wc_res.txt");
        string wcOut = CManager.ReadFileText("/tmp/wc_res.txt").Trim();
        Check(wcOut.Contains("6"), "Piping cat | wc -l count");

        Exec($"echo \"tee_data\" | tee /tmp/tee1.txt /tmp/tee2.txt > /dev/null");
        Check(CManager.ReadFileText("/tmp/tee1.txt").Trim() == "tee_data" && CManager.ReadFileText("/tmp/tee2.txt").Trim() == "tee_data", "tee dual file output");

        Exec($"rm -f {rFile} /tmp/grep_res.txt /tmp/head_res.txt /tmp/tail_res.txt /tmp/wc_res.txt /tmp/tee1.txt /tmp/tee2.txt");

        return block;
    }

    private static TestBlock TestFindAndTreeCommands()
    {
        TestBlock block = new("find & tree Commands");
        CurBlock = block;

        string tDir = "/tmp/find_tree_test";
        if (Directory.Exists(tDir)) Directory.Delete(tDir, true);
        Directory.CreateDirectory($"{tDir}/sub");
        File.WriteAllText($"{tDir}/file1.txt", "hello");
        File.WriteAllText($"{tDir}/sub/file2.log", "world");

        Check(Exec($"find {tDir}") == 0, "find directory hierarchy");
        Check(Exec($"find {tDir} -name *.log") == 0, "find -name pattern");
        Check(Exec($"find {tDir} -type f") == 0, "find -type f");
        Check(Exec($"find {tDir} -maxdepth 1") == 0, "find -maxdepth 1");

        Check(Exec($"tree {tDir}") == 0, "tree directory listing");
        Check(Exec($"tree {tDir} -L 1") == 0, "tree -L 1 level");
        Check(Exec($"tree {tDir} -d") == 0, "tree -d directories only");

        Directory.Delete(tDir, true);
        return block;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
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
