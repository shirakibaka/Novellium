// Init.cs — Kernel subsystem, ext2 filesystem, and service initialization
using System;
using System.Text;
using Cosmos.Kernel.HAL.Interfaces.Devices;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Filesystems.Ext2;
using Cosmos.Kernel.System.Storage;
using Cosmos.Kernel.System.Vfs;
using Novellium.Commands;
using Novellium.IO;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.System;

public static class Init
{
    private const string FsDriver = "ext2";
    private const string MountPoint = "/";
    private const ulong TargetDiskBytes = 512UL * 1024UL * 1024UL; // 512 MiB

    public static bool IsBoot { get; private set; }

    public static void Start()
    {
        if (IsBoot) return;

        OutputInfo.Info("Starting Novellium");

        OutputInfo.Info("Initializing process manager...");
        if (!PManager.Initialize())
            OutputInfo.Error("Process manager has not been initialized correctly.");

        JManager.Init();

        OutputInfo.Info("Initializing filesystem...");
        InitFs();

        OutputInfo.Info("Initializing services...");
        OutputInfo.Info("Starting syslog daemon...");
        int syslogPid = Syslogd.Start();
        if (syslogPid > 0) OutputInfo.Ok($"syslogd started (PID {syslogPid})");
        else OutputInfo.Warning("Failed to start syslogd");

        IsBoot = true;
        OutputInfo.Ok("System initialization complete.");
    }

    private static void InitFs()
    {
        OutputInfo.Info($"Block devices: {StorageManager.Devices.Count}");
        OutputInfo.Info($"Partitions: {StorageManager.Partitions.Count}");
        PrintDisks();

        IBlockDevice? disk = FindDisk();
        if (disk == null)
        {
            OutputInfo.Warning("512 MiB storage device was not found.");
            return;
        }

        Ext2FilesystemType ext2 = new(disk);
        if (!VfsManager.RegisterFilesystem(FsDriver, ext2))
        {
            OutputInfo.Error("Failed to register ext2 filesystem driver.");
            return;
        }
        OutputInfo.Ok("ext2 filesystem driver registered.");

        if (!VfsManager.TryMount(FsDriver, "", MountFlags.None, MountPoint, out var mounted))
        {
            OutputInfo.Error($"Failed to mount ext2 at {MountPoint}");
            return;
        }
        OutputInfo.Ok($"ext2 mounted at {mounted.MountPoint}");

        if (!VfsManager.TryStatFs(MountPoint, out _))
            OutputInfo.Warning("Mounted ext2, but filesystem statistics are unavailable.");

        InitFsTree();
    }

    private static void InitFsTree()
    {
        if (VfsManager.TryStat("/etc/version", out _) && VfsManager.TryStat("/bin", out var binStat) && binStat.IsDirectory)
        {
            OutputInfo.Ok("Root filesystem structure already exists.");
            return;
        }

        OutputInfo.Info("Initializing root filesystem structure...");
        MakeDir("/bin", (VfsMode)493);
        MakeDir("/etc", (VfsMode)493);
        MakeDir("/home", (VfsMode)493);
        MakeDir("/home/user", (VfsMode)493);
        MakeDir("/var", (VfsMode)493);
        MakeDir("/var/log", (VfsMode)493);
        MakeDir("/tmp", (VfsMode)511);
        MakeDir("/root", (VfsMode)448);
        MakeDir("/dev", (VfsMode)493);
        MakeDir("/proc", (VfsMode)493);
        MakeDir("/usr", (VfsMode)493);

        MakeFile("/etc/hostname", "novellium\n");
        MakeFile("/etc/os-release", "NAME=Novellium\nID=novellium\nVERSION=\"0.1.0\"\nPRETTY_NAME=\"Novellium\"\n");
        MakeFile("/etc/motd", "Welcome to Novellium!\n");
        MakeFile("/etc/version", "0.1.0 (Cosmos 3.0.88 / Limine 8.0.2)\n");

        OutputInfo.Ok("Root filesystem structure initialized.");
    }

    private static void MakeDir(string path, VfsMode mode)
    {
        if (VfsManager.TryStat(path, out VfsStat stat))
        {
            if (!stat.IsDirectory) OutputInfo.Warning($"Path exists but is not a directory: {path}");
            return;
        }

        if (VfsManager.TryCreateDirectory(path, mode))
            OutputInfo.Ok($"Directory created: {path}");
        else
            OutputInfo.Error($"Failed to create directory: {path}");
    }

    private static void MakeFile(string path, string content) => MakeFile(path, content, (VfsMode)420);

    private static void MakeFile(string path, string content, VfsMode mode)
    {
        if (VfsManager.TryStat(path, out _)) return;

        if (!VfsManager.TryCreateFile(path, mode))
        {
            OutputInfo.Error($"Failed to create file: {path}");
            return;
        }

        if (!string.IsNullOrEmpty(content))
        {
            if (VfsManager.TryOpenFile(path, out var handle) && handle != null)
            {
                using (handle) handle.Write(Encoding.UTF8.GetBytes(content));
            }
            else
            {
                OutputInfo.Warning($"Created file {path}, but failed to write initial content.");
            }
        }

        OutputInfo.Ok($"File created: {path}");
    }

    private static IBlockDevice? FindDisk()
    {
        foreach (IBlockDevice dev in StorageManager.Devices)
        {
            if (dev.BlockCount * dev.BlockSize == TargetDiskBytes)
                return dev;
        }
        return null;
    }

    private static void PrintDisks()
    {
        if (StorageManager.Devices.Count == 0)
        {
            OutputInfo.Info("No block devices detected.");
            return;
        }

        Output.WriteLine("Detected block devices:", ConsoleColor.Gray);
        foreach (IBlockDevice dev in StorageManager.Devices)
        {
            ulong totalMiB = (dev.BlockCount * dev.BlockSize) / (1024UL * 1024UL);
            Output.WriteLine($"  {dev.Name} {totalMiB} MiB ({dev.BlockCount} blocks × {dev.BlockSize} bytes)", ConsoleColor.Gray);
        }
    }

    public static void ShowMotd()
    {
        Output.Clear();
        PrintFile("/etc/motd", ConsoleColor.Yellow);
        PrintFile("/etc/version", ConsoleColor.Gray);
        Output.WriteLine();
    }

    public static void PrintFile(string path, ConsoleColor color)
    {
        string text = CManager.ReadFileText(path).TrimEnd('\r', '\n');
        if (!string.IsNullOrEmpty(text))
            Output.WriteLine(text, color);
    }
}
