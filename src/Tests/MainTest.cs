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
using Novellium.IO.Cache;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.Tests;

public static class MainTest
{
    private static int Passed, Failed;
    private static readonly List<string> FailedTests = new();
    private static TestBlock? CurBlock;
    private static string CurBlockName = "";
    private static int LastLineLen = 0;

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
        Output.WriteDirectLine("Running Novellium Test Suite...", ConsoleColor.White);

        string oldCwd = CManager.CurrentDirectory;
        bool prevAutoReap = PManager.AutomaticOrphanReaping;
        PManager.AutomaticOrphanReaping = false;

        bool prevPauseSyslog = Syslogd.PauseDiskFlushing;
        Syslogd.PauseDiskFlushing = true;

        List<TestBlock> blocks = new();
        Output.StartCapture();
        try
        {
            blocks.Add(RunBlock("Kernel & Core Subsystems", TestKernelAndSubsystems));
            blocks.Add(RunBlock("Process & Job Lifecycle", TestProcessLifecycle));
            blocks.Add(RunBlock("Path Normalization & Resolution", TestPathNormalization));
            blocks.Add(RunBlock("Ext2 Filesystem Engine", TestExt2Filesystem));
            blocks.Add(RunBlock("End-To-End Developer Workflow", TestE2EDeveloperWorkflow));
            blocks.Add(RunBlock("cd & pwd Utility Tests", TestCdAndPwdCommands));
            blocks.Add(RunBlock("File & Directory Operations", TestFileCommands));
            blocks.Add(RunBlock("System Info & Utilities", TestSystemInfoUtilities));
            blocks.Add(RunBlock("Unix Utilities & Redirection", TestUnixCoreutilsAndRedirection));
            blocks.Add(RunBlock("find & tree Commands", TestFindAndTreeCommands));
            blocks.Add(RunBlock("cp & mv Commands", TestCpAndMvCommands));
            blocks.Add(RunBlock("Clock Cache Engine", TestClockCache));
            blocks.Add(RunBlock("fallocate, dd & /dev/urandom", TestFallocateAndDdCommands));
            blocks.Add(RunBlock("VFS Global 128MB Cache Integration", TestVfsGlobalCache));
        }
        finally
        {
            Output.StopCapture();
            Syslogd.PauseDiskFlushing = prevPauseSyslog;
            PManager.AutomaticOrphanReaping = prevAutoReap;
            CManager.CurrentDirectory = oldCwd;
        }

        Output.WriteDirectLine();
        if (Failed == 0)
        {
            Output.WriteDirect("Test suite result: ", ConsoleColor.White);
            Output.WriteDirect($"{Passed} passed", ConsoleColor.Green);
            Output.WriteDirectLine(", 0 failed.", ConsoleColor.White);
        }
        else
        {
            Output.WriteDirect("Test suite result: ", ConsoleColor.White);
            Output.WriteDirect($"{Passed} passed", ConsoleColor.Green);
            Output.WriteDirect(", ", ConsoleColor.White);
            Output.WriteDirect($"{Failed} failed", ConsoleColor.Red);
            Output.WriteDirectLine(".", ConsoleColor.White);

            Output.WriteDirectLine("Failed tests:", ConsoleColor.White);
            foreach (string f in FailedTests)
            {
                Output.WriteDirect("  - ", ConsoleColor.White);
                Output.WriteDirectLine(f, ConsoleColor.Red);
            }
        }
        Output.WriteDirectLine();
    }

    private static TestBlock RunBlock(string title, Func<TestBlock> func)
    {
        CurBlockName = title;
        string startText = $"Running suite: {title}...";
        Output.WriteDirect("  [", ConsoleColor.White);
        Output.WriteDirect("+", ConsoleColor.Yellow);
        Output.WriteDirect("] ", ConsoleColor.White);
        Output.WriteDirect(startText, ConsoleColor.Gray);
        LastLineLen = startText.Length;

        TestBlock b;
        try
        {
            b = func();
        }
        catch (Exception ex)
        {
            b = CurBlock ?? new TestBlock(title);
            Failed++;
            FailedTests.Add($"{title}: {ex.Message}");
            b.Items.Add((false, $"Suite exception: {ex.Message}"));
        }

        int passedCount = 0;
        int failedCount = 0;
        foreach (var item in b.Items)
        {
            if (item.Passed) passedCount++;
            else failedCount++;
        }

        string icon;
        ConsoleColor iconColor;
        string compText;

        if (failedCount == 0)
        {
            icon = "v";
            iconColor = ConsoleColor.Green;
            compText = $"{b.Title} ({passedCount} tests passed)";
        }
        else if (passedCount > 0)
        {
            icon = "v";
            iconColor = ConsoleColor.Yellow;
            compText = $"{b.Title} ({passedCount}/{b.Items.Count} passed)";
        }
        else
        {
            icon = "x";
            iconColor = ConsoleColor.Red;
            compText = $"{b.Title} ({failedCount} tests failed)";
        }

        int pad = Math.Max(0, LastLineLen - compText.Length);
        Output.WriteDirect("\r  [", ConsoleColor.White);
        Output.WriteDirect(icon, iconColor);
        Output.WriteDirect("] ", ConsoleColor.White);
        Output.WriteDirectLine(compText + new string(' ', pad), ConsoleColor.White);

        if (failedCount > 0)
        {
            for (int i = 0; i < b.Items.Count; i++)
            {
                bool isLast = (i == b.Items.Count - 1);
                var (passed, name) = b.Items[i];
                string connector = isLast ? "`-- " : "|-- ";

                Output.WriteDirect("     ", ConsoleColor.White);
                Output.WriteDirect(connector, ConsoleColor.Gray);
                Output.WriteDirect("[", ConsoleColor.White);
                Output.WriteDirect(passed ? "v" : "x", passed ? ConsoleColor.Green : ConsoleColor.Red);
                Output.WriteDirect("] ", ConsoleColor.White);
                Output.WriteDirectLine(name, passed ? ConsoleColor.White : ConsoleColor.White);
            }
        }

        return b;
    }

    private static TestBlock TestKernelAndSubsystems()
    {
        TestBlock block = new("Kernel & Core Subsystems");
        CurBlock = block;

        PInfo? k = PManager.Get(1);
        Check(k != null && k.ParentPid == 0 && k.State == PState.Running, "Kernel PID 1 state");
        Check(!PManager.Kill(1), "Kernel process immortality");

        Syslogd.Log(LogLevel.Info, "main_test", "Master test suite syslog entry");
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

        string[] qArgs = CManager.SplitArgs("echo \"hello world\" 'foo bar'");
        Check(qArgs.Length == 3 && qArgs[1] == "hello world" && qArgs[2] == "foo bar", "SplitArgs quoted string parsing");
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

        Exec("cd /");
        Exec($"rm -f {ws}/Makefile {ws}/README.txt {ws}/src/main.c {ws}/src/utils.c {bgApp}");
        Exec($"rmdir {ws}/src {ws}/docs {ws}/build");
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

        string[] topics = ["ls", "cat", "cd", "pwd", "touch", "mkdir", "rm", "rmdir", "df", "stat", "uname", "uptime", "free", "ps", "jobs", "kill", "wait", "sleep", "help", "dmesg", "clear", "test", "find", "tree", "cp", "mv"];
        bool helpOk = true;
        foreach (string t in topics)
        {
            if (Exec($"help {t}") != 0) { helpOk = false; break; }
        }
        Check(helpOk, "help builtin topics (26)");

        string[] cmdHelps = [
            "ps --help", "jobs -h", "kill --help", "wait -h", "sleep --help",
            "dmesg -h", "cat --help", "cd -h", "pwd --help", "touch -h",
            "mkdir --help", "rm -h", "rmdir --help", "stat --help", "uname -h",
            "uptime --help", "free -h", "clear -h", "clear --help", "test -h", "test --help",
            "find --help", "find -h", "tree --help", "tree -h",
            "cp --help", "cp -h", "mv --help", "mv -h"
        ];
        bool flagsOk = true;
        foreach (string c in cmdHelps)
        {
            if (Exec(c) != 0) { flagsOk = false; break; }
        }
        Check(flagsOk, "Universal -h/--help flags (29)");

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

        // Test && conditional execution (success case)
        string andFile1 = "/tmp/and_test1.txt";
        Exec($"touch {andFile1} && echo \"AndSuccess\" > /tmp/and_out.txt");
        Check(CManager.ReadFileText("/tmp/and_out.txt").Contains("AndSuccess"), "&& conditional AND execution on success");

        // Test && conditional execution (failure skip case)
        string andFile2 = "/tmp/and_out2.txt";
        if (File.Exists(andFile2)) File.Delete(andFile2);
        Exec($"cat /nonexistent_file_xyz.dat && echo \"ShouldNotRun\" > {andFile2}");
        Check(!File.Exists(andFile2), "&& conditional AND skips command on failure");

        // Test ; sequential execution
        string seqFile = "/tmp/seq_out.txt";
        Exec($"echo \"Part1\" > {seqFile} ; echo \"Part2\" >> {seqFile}");
        Check(CManager.ReadFileText(seqFile).Contains("Part2"), "; sequential execution operator");

        // Test & background execution
        string bgFile = "/tmp/bg_out.dat";
        Check(Exec($"dd if=/dev/urandom of={bgFile} bs=512 count=4 &") >= 0, "& background async job launch");

        // Test < input redirection
        Exec($"grep alpha < {rFile} > /tmp/stdin_res.txt");
        Check(CManager.ReadFileText("/tmp/stdin_res.txt").Trim() == "alpha", "< input redirection operator");

        Exec($"rm -f {rFile} /tmp/grep_res.txt /tmp/head_res.txt /tmp/tail_res.txt /tmp/wc_res.txt /tmp/tee1.txt /tmp/tee2.txt {andFile1} /tmp/and_out.txt /tmp/and_out2.txt {seqFile} {bgFile} /tmp/stdin_res.txt");

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

    private static TestBlock TestCpAndMvCommands()
    {
        TestBlock block = new("cp & mv Commands");
        CurBlock = block;

        string cDir = "/tmp/cpmv_test";
        if (Directory.Exists(cDir)) Directory.Delete(cDir, true);
        Directory.CreateDirectory($"{cDir}/src_dir");

        string srcFile = $"{cDir}/src_dir/orig.txt";
        File.WriteAllText(srcFile, "Cp Mv Content");

        string copyFile = $"{cDir}/src_dir/copy.txt";
        Check(Exec($"cp {srcFile} {copyFile}") == 0 && File.Exists(copyFile) && File.ReadAllText(copyFile) == "Cp Mv Content", "cp single file");

        string backupDir = $"{cDir}/backup";
        Check(Exec($"cp -r {cDir}/src_dir {backupDir}") == 0 && Directory.Exists(backupDir) && File.Exists($"{backupDir}/orig.txt"), "cp -r directory copy");

        string movedFile = $"{cDir}/src_dir/moved.txt";
        Check(Exec($"mv {copyFile} {movedFile}") == 0 && !File.Exists(copyFile) && File.Exists(movedFile), "mv rename file");

        string movedDir = $"{cDir}/moved_backup";
        Check(Exec($"mv {backupDir} {movedDir}") == 0 && !Directory.Exists(backupDir) && Directory.Exists(movedDir), "mv directory move");

        Directory.Delete(cDir, true);
        return block;
    }

    private static TestBlock TestClockCache()
    {
        TestBlock block = new("Clock Cache Engine");
        CurBlock = block;

        // 1. Basic fill, hit & miss check
        BlockCacheEngine cache = new(4, 512, new ClockPolicy());
        byte[] dummy = new byte[] { 1, 2, 3 };

        cache.Put(10, dummy);
        cache.Put(20, dummy);
        cache.Put(30, dummy);
        cache.Put(40, dummy);

        Check(cache.Count == 4 && cache.ContainsKey(10) && cache.ContainsKey(40), "BlockCacheEngine initial 4-item fill");
        Check(cache.TryGet(10, out var d1) && d1 != null && cache.TryGet(20, out _) && cache.Hits == 2, "BlockCacheEngine hit verification & counter");

        // 2. Second Chance Bit & Eviction (10 and 20 were accessed, so 30 is evicted)
        cache.Put(50, dummy);
        Check(!cache.ContainsKey(30) && cache.ContainsKey(10) && cache.ContainsKey(20) && cache.ContainsKey(50) && cache.Evictions == 1, "BlockCacheEngine eviction of unreferenced slot");

        // 3. Sequential Scan Workload Simulation (32 keys on capacity 8)
        BlockCacheEngine scanCache = new(8, 512, new ClockPolicy());
        for (ulong i = 1; i <= 32; i++)
        {
            if (!scanCache.TryGet(i, out _))
            {
                scanCache.Put(i, dummy);
            }
        }
        Check(scanCache.Misses == 32 && scanCache.Hits == 0 && scanCache.Evictions == 24, "BlockCacheEngine sequential scan workload (0% hits)");

        // 4. Hotspot / Zipfian Workload Simulation (80% requests hit hot keys 1..3, 20% hit 4..20)
        BlockCacheEngine hotCache = new(8, 512, new ClockPolicy());
        for (ulong i = 1; i <= 8; i++) hotCache.Put(i, dummy);
        hotCache.ResetStats();

        Random rnd = new(42);
        for (int req = 0; req < 100; req++)
        {
            ulong key = (rnd.Next(100) < 80) ? (ulong)rnd.Next(1, 4) : (ulong)rnd.Next(4, 21);
            if (!hotCache.TryGet(key, out _))
            {
                hotCache.Put(key, dummy);
            }
        }
        Check(hotCache.HitRatio >= 70.0, "BlockCacheEngine hotspot workload high hit ratio");

        // 5. Clear & ResetState
        hotCache.Clear();
        Check(hotCache.Count == 0 && !hotCache.ContainsKey(1), "BlockCacheEngine Clear resets capacity and items");

        return block;
    }

    private static TestBlock TestFallocateAndDdCommands()
    {
        TestBlock block = new("fallocate, dd & /dev/urandom");
        CurBlock = block;

        string workDir = "/tmp/fdd_test";
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        string fallocFile = $"{workDir}/allocated.dat";
        Check(Exec($"fallocate -l 4K {fallocFile}") == 0 && File.Exists(fallocFile) && new FileInfo(fallocFile).Length == 4096, "fallocate 4K preallocation");

        string randFile = $"{workDir}/rand.dat";
        Check(Exec($"dd if=/dev/urandom of={randFile} bs=512 count=8") == 0 && File.Exists(randFile) && new FileInfo(randFile).Length == 4096, "dd /dev/urandom generation (4KB)");

        string copyFile = $"{workDir}/copy.dat";
        Novellium.IO.Cache.VfsCacheEngine.Engine.ResetStats();

        // 1st Read of rand.dat (populates VFS cache)
        Check(Exec($"dd if={randFile} of={copyFile} bs=512 count=8") == 0 && File.Exists(copyFile) && new FileInfo(copyFile).Length == 4096, "dd file copy (1st read populates cache)");
        long hits1 = Novellium.IO.Cache.VfsCacheEngine.Engine.Hits;

        // 2nd Read of rand.dat (hits VFS block cache!)
        Check(Exec($"cat {randFile}") == 0, "cat cached file (2nd read hits VFS cache)");
        long hits2 = Novellium.IO.Cache.VfsCacheEngine.Engine.Hits;

        Check(hits2 > hits1, "VFS BlockCache hit verification on 2nd file read");

        Directory.Delete(workDir, true);
        return block;
    }

    private static TestBlock TestVfsGlobalCache()
    {
        TestBlock block = new("VFS Global 128MB Cache Integration");
        CurBlock = block;

        // 1. Capacity & Lazy Allocation Verification
        var eng = Novellium.IO.Cache.VfsCacheEngine.Engine;
        Check(eng.Capacity == 32768 && eng.BlockSize == 4096, "VFS Cache 128MB capacity (32,768 slots x 4KB)");

        string testFile = "/tmp/cache_global_test.txt";
        if (File.Exists(testFile)) File.Delete(testFile);

        // 2. Write file
        File.WriteAllText(testFile, "Line 1: Novellium OS\nLine 2: Fast RAM Cache\nLine 3: 128MB Pool\n");

        eng.ResetStats();
        long initialHits = eng.Hits;

        // 3. 1st Read via grep (populates VFS page cache)
        Check(Exec($"grep Novellium {testFile}") == 0, "grep 1st read (populates RAM cache)");

        // 4. 2nd Read via head (hits VFS page cache!)
        Check(Exec($"head -n 2 {testFile}") == 0, "head 2nd read");

        // 5. 3rd Read via wc (hits VFS page cache!)
        Check(Exec($"wc -l {testFile}") == 0, "wc 3rd read");

        long finalHits = eng.Hits;
        Check(finalHits > initialHits, "Global VFS cache hits on grep/head/wc utility chain");

        // 6. Overwrite invalidation test
        Check(Exec($"echo \"Updated Content\" > {testFile}") == 0, "echo > file overwrite invalidates cache");
        Check(CManager.ReadFileText(testFile).Contains("Updated Content"), "ReadFileText reads updated content after invalidation");

        if (File.Exists(testFile)) File.Delete(testFile);
        return block;
    }

    private static void Check(bool condition, string name)
    {
        string runText = $"Running: {CurBlockName} -> {name}...";
        int pad = Math.Max(0, LastLineLen - runText.Length);
        Output.WriteDirect("\r  [", ConsoleColor.White);
        Output.WriteDirect("+", ConsoleColor.Yellow);
        Output.WriteDirect("] ", ConsoleColor.White);
        Output.WriteDirect(runText + new string(' ', pad), ConsoleColor.Gray);
        LastLineLen = runText.Length;

        if (condition)
        {
            Passed++;
            CurBlock?.Items.Add((true, name));
        }
        else
        {
            Failed++;
            FailedTests.Add($"{CurBlockName}: {name}");
            CurBlock?.Items.Add((false, name));
        }
    }
}
