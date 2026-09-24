// Ext2Tests.cs — Ext2 filesystem driver test suite
using System;
using System.Collections.Generic;
using System.IO;
using Cosmos.Kernel.HAL.Interfaces.Devices;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Filesystems.Ext2;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;

namespace Novellium.Tests;

public static class Ext2Tests
{
    private static int Passed;
    private static int Failed;
    private static readonly List<string> FailedTests = new();

    public static void Run()
    {
        Passed = 0;
        Failed = 0;
        FailedTests.Clear();

        Output.WriteLine("=== EXT2 TESTS ===", ConsoleColor.Cyan);

        TestRamFilesystem();

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

    private static void TestRamFilesystem()
    {
        MemoryBlockDevice dev = new("EXT2TEST", 512, 32768); // 16 MiB
        Ext2FilesystemType ext2 = new(dev);

        Check(VfsManager.RegisterFilesystem("ext2test", ext2), "ext2 driver registered");
        Check(VfsManager.TryFormat("ext2test", "", new Ext2FormatOptions { BlockSize = 1024, VolumeLabel = "NOVELLIUM" }), "ext2 filesystem formatted");
        Check(VfsManager.TryMount("ext2test", "", MountFlags.None, "/ext2test", out _), "ext2 filesystem mounted");

        Check(Directory.Exists("/ext2test"), "mount point exists");

        Directory.CreateDirectory("/ext2test/home");
        Check(Directory.Exists("/ext2test/home"), "directory creation");

        File.WriteAllText("/ext2test/home/test.txt", "Novellium ext2 test");
        Check(File.Exists("/ext2test/home/test.txt"), "file creation");
        Check(File.ReadAllText("/ext2test/home/test.txt") == "Novellium ext2 test", "file read/write");

        File.AppendAllText("/ext2test/home/test.txt", "\nsecond line");
        Check(File.ReadAllText("/ext2test/home/test.txt") == "Novellium ext2 test\nsecond line", "file append");

        File.Move("/ext2test/home/test.txt", "/ext2test/home/renamed.txt");
        Check(!File.Exists("/ext2test/home/test.txt") && File.Exists("/ext2test/home/renamed.txt"), "file rename");

        File.Delete("/ext2test/home/renamed.txt");
        Check(!File.Exists("/ext2test/home/renamed.txt"), "file delete");

        Check(VfsManager.TryStatFs("/ext2test", out var stats), "ext2 statfs");
        Check(stats.Blocks > 0 && stats.Bavail > 0, "ext2 free space");

        Check(VfsManager.TryUnmount("/ext2test"), "ext2 unmount");
        Check(VfsManager.TryMount("ext2test", "", MountFlags.None, "/ext2test", out _), "ext2 remount");
        Check(!File.Exists("/ext2test/home/renamed.txt"), "deleted file stays deleted after remount");

        Directory.CreateDirectory("/ext2test/persistence");
        File.WriteAllText("/ext2test/persistence/persist.txt", "persistent data");

        Check(VfsManager.TryUnmount("/ext2test"), "second ext2 unmount");
        Check(VfsManager.TryMount("ext2test", "", MountFlags.None, "/ext2test", out _), "second ext2 remount");
        Check(File.ReadAllText("/ext2test/persistence/persist.txt") == "persistent data", "data survives remount");

        VfsManager.TryUnmount("/ext2test");
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

    private sealed class MemoryBlockDevice : IBlockDevice
    {
        private readonly byte[] Storage;

        public MemoryBlockDevice(string name, ulong blockSize, ulong blockCount)
        {
            Name = name;
            BlockSize = blockSize;
            BlockCount = blockCount;
            Storage = new byte[checked((int)(blockSize * blockCount))];
        }

        public string Name { get; }
        public ulong BlockSize { get; }
        public ulong BlockCount { get; }

        public void ReadBlock(ulong blockNo, ulong blockCount, Span<byte> data)
        {
            int off = checked((int)(blockNo * BlockSize));
            int len = checked((int)(blockCount * BlockSize));
            Storage.AsSpan(off, len).CopyTo(data);
        }

        public void WriteBlock(ulong blockNo, ulong blockCount, ReadOnlySpan<byte> data)
        {
            int off = checked((int)(blockNo * BlockSize));
            int len = checked((int)(blockCount * BlockSize));
            data.Slice(0, len).CopyTo(Storage.AsSpan(off, len));
        }

        public void Flush() { }
    }
}