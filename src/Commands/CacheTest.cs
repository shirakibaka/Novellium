// CacheTest.cs — cache command for block cache management, benchmarks, and stats
using System;
using Novellium.IO;
using Novellium.IO.Cache;
using Novellium.Process;
using Novellium.Services;

namespace Novellium.Commands;

public static class CacheTest
{
    public static void Run(int pid, string[] args)
    {
        if (args.Length <= 1)
        {
            Help();
            return;
        }

        string cmd = args[1].ToLowerInvariant();
        switch (cmd)
        {
            case "-s":
            case "--stats":
            case "stats":
                ShowStats();
                break;

            case "-l":
            case "--latency":
            case "latency":
                string file = (args.Length > 2) ? args[2] : "/tmp/rand_data.dat";
                int iters = (args.Length > 3 && int.TryParse(args[3], out int it)) ? Math.Max(1, it) : 100;
                MeasureLatency(pid, file, iters);
                break;

            case "-b":
            case "--bench":
            case "bench":
                int capacity = 8;
                if (args.Length > 2 && int.TryParse(args[2], out int cap)) capacity = Math.Max(1, cap);
                RunBenchmark(capacity);
                break;

            case "-f":
            case "--flush":
            case "flush":
                VfsCacheEngine.Clear();
                Output.Write("Block cache flushed: ", ConsoleColor.Gray);
                Output.WriteLine("OK", ConsoleColor.Green);
                break;

            case "--fill":
            case "fill":
                int fillCount = 8;
                if (args.Length > 2 && int.TryParse(args[2], out int fc)) fillCount = Math.Max(1, fc);
                FillCache(fillCount);
                break;

            case "--on":
                VfsCacheEngine.Enabled = true;
                Output.Write("VFS Block Cache: ", ConsoleColor.Gray);
                Output.WriteLine("enabled", ConsoleColor.Green);
                break;

            case "--off":
                VfsCacheEngine.Enabled = false;
                Output.Write("VFS Block Cache: ", ConsoleColor.Gray);
                Output.WriteLine("disabled", ConsoleColor.Red);
                break;

            case "-v":
            case "--verbose":
                VfsCacheEngine.Verbose = !VfsCacheEngine.Verbose;
                Output.Write("Cache Verbose Diagnostics: ", ConsoleColor.Gray);
                if (VfsCacheEngine.Verbose) Output.WriteLine("on", ConsoleColor.Green);
                else Output.WriteLine("off", ConsoleColor.Red);
                break;

            case "-h":
            case "--help":
            case "help":
            default:
                if (cmd.StartsWith('-') && cmd is not ("-h" or "--help"))
                {
                    Output.WriteLine($"cache: invalid option -- '{cmd}'", ConsoleColor.Red);
                    Output.WriteLine("Try 'cache --help' for more information.", ConsoleColor.Gray);
                    PManager.Exit(pid, 1);
                }
                else
                {
                    Help();
                }
                break;
        }
    }

    private static void MeasureLatency(int pid, string filePath, int iterations)
    {
        string path = CManager.ResolvePath(filePath);
        if (!Cosmos.Kernel.System.Vfs.VfsManager.TryStat(path, out var st) || st.IsDirectory)
        {
            Output.WriteLine($"cache: '{filePath}': No such file or directory", ConsoleColor.Red);
            PManager.Exit(pid, 1);
            return;
        }

        // Calculate 4KB page count
        ulong pages = Math.Max(1UL, (st.Size + 4095UL) / 4096UL);
        int effectiveIters = iterations;
        
        // Cap total block reads during cold disk benchmark to max 500 operations to prevent AHCI bus lockup
        if (pages * (ulong)effectiveIters > 500UL)
        {
            effectiveIters = Math.Max(1, (int)(500UL / pages));
        }

        Output.WriteLine($"Measuring I/O Latency for '{filePath}' ({st.Size:N0} Bytes, {effectiveIters} iterations)...", ConsoleColor.White);

        // Pause Syslogd background disk flushing during test to prevent Ext2 thread contention freeze
        bool prevSyslogPause = Syslogd.PauseDiskFlushing;
        Syslogd.PauseDiskFlushing = true;

        bool prevEnabled = VfsCacheEngine.Enabled;

        try
        {
            // 1. Cold Ext2 Disk Access (Cache Disabled)
            VfsCacheEngine.Enabled = false;
            VfsCacheEngine.Clear();

            long startDisk = Environment.TickCount64;
            for (int i = 0; i < effectiveIters; i++)
            {
                if (Cosmos.Kernel.System.Vfs.VfsManager.TryOpenFile(path, out var h) && h != null)
                {
                    using (h)
                    {
                        byte[] buf = new byte[(int)st.Size];
                        h.Read(buf);
                    }
                }
            }
            long diskTimeMs = Math.Max(1, Environment.TickCount64 - startDisk);

            // 2. RAM BlockCache Access (Cache Enabled & Warm)
            VfsCacheEngine.Enabled = true;
            VfsCacheEngine.Clear();
            VfsCacheEngine.TryReadFile(path, out _); // Warm up cache

            long startRam = Environment.TickCount64;
            for (int i = 0; i < effectiveIters; i++)
            {
                VfsCacheEngine.TryReadFile(path, out _);
            }
            long ramTimeMs = Environment.TickCount64 - startRam;

            double speedup = (double)diskTimeMs / Math.Max(1, ramTimeMs);

            Output.WriteLine("Latency Benchmark Results:", ConsoleColor.White);
            Output.Write("  Ext2 Disk Read Time : ", ConsoleColor.Gray);
            Output.WriteLine($"{diskTimeMs} ms ({(diskTimeMs / (double)effectiveIters):F3} ms/op)", ConsoleColor.White);

            Output.Write("  RAM BlockCache Time : ", ConsoleColor.Gray);
            Output.WriteLine($"{ramTimeMs} ms ({(ramTimeMs / (double)effectiveIters):F3} ms/op)", ConsoleColor.White);

            Output.Write("  Speedup Ratio       : ", ConsoleColor.Gray);
            Output.WriteLine($"{speedup:F1}x", ConsoleColor.White);
        }
        finally
        {
            VfsCacheEngine.Enabled = prevEnabled;
            Syslogd.PauseDiskFlushing = prevSyslogPause;
        }
    }

    private static void ShowStats()
    {
        var eng = VfsCacheEngine.Engine;
        Output.WriteLine("Cache Statistics:", ConsoleColor.White);

        Output.Write("  Policy           : ", ConsoleColor.Gray);
        Output.WriteLine(eng.ActivePolicy.Name, ConsoleColor.White);

        Output.Write("  Status           : ", ConsoleColor.Gray);
        if (VfsCacheEngine.Enabled) Output.WriteLine("enabled", ConsoleColor.Green);
        else Output.WriteLine("disabled", ConsoleColor.Red);

        Output.Write("  Verbose Trace    : ", ConsoleColor.Gray);
        if (VfsCacheEngine.Verbose) Output.WriteLine("on", ConsoleColor.Green);
        else Output.WriteLine("off", ConsoleColor.DarkGray);

        Output.Write("  Block Config     : ", ConsoleColor.Gray);
        Output.WriteLine($"{eng.BlockSize} Bytes / block", ConsoleColor.White);

        Output.Write("  Occupied Slots   : ", ConsoleColor.Gray);
        Output.WriteLine($"{eng.Count} / {eng.Capacity} slots", ConsoleColor.White);

        Output.Write("  Payload Memory   : ", ConsoleColor.Gray);
        Output.Write($"{eng.UsedPayloadBytes:N0} Bytes", ConsoleColor.Green);
        Output.WriteLine($" ({eng.UsedPayloadBits:N0} bits)", ConsoleColor.Gray);

        Output.Write("  Pool Capacity    : ", ConsoleColor.Gray);
        Output.Write($"{eng.TotalCapacityBytes:N0} Bytes", ConsoleColor.White);
        Output.WriteLine($" ({eng.TotalCapacityBits:N0} bits)", ConsoleColor.Gray);

        Output.Write("  Metadata Overhead: ", ConsoleColor.Gray);
        Output.WriteLine($"~{eng.MetadataOverheadBytes:N0} Bytes", ConsoleColor.DarkGray);

        Output.Write("  Hits             : ", ConsoleColor.Gray);
        Output.WriteLine(eng.Hits.ToString("N0"), ConsoleColor.Green);

        Output.Write("  Misses           : ", ConsoleColor.Gray);
        Output.WriteLine(eng.Misses.ToString("N0"), ConsoleColor.Yellow);

        Output.Write("  Evictions        : ", ConsoleColor.Gray);
        Output.WriteLine(eng.Evictions.ToString("N0"), ConsoleColor.DarkGray);

        Output.Write("  Hit Ratio        : ", ConsoleColor.Gray);
        ConsoleColor pctColor = eng.HitRatio >= 70.0 ? ConsoleColor.Green : (eng.HitRatio > 0 ? ConsoleColor.Yellow : ConsoleColor.Red);
        Output.WriteLine($"{eng.HitRatio:F1}%", pctColor);
    }

    private static void FillCache(int count)
    {
        var eng = VfsCacheEngine.Engine;
        byte[] dummy = new byte[eng.BlockSize];
        int added = 0;
        for (int i = 1; i <= count; i++)
        {
            ulong key = VfsCacheEngine.ComputeKey($"/sys/test_block_{i}.dat");
            dummy[0] = (byte)(i & 0xFF);
            eng.Put(key, dummy);
            added++;
        }

        Output.Write("Filled cache blocks: ", ConsoleColor.Gray);
        Output.Write($"{added} blocks", ConsoleColor.Green);
        Output.WriteLine($" ({eng.Count}/{eng.Capacity} total used)", ConsoleColor.Gray);
    }

    private static void RunBenchmark(int capacity)
    {
        Output.WriteLine($"Cache Benchmark (Clock, Capacity: {capacity} blocks)", ConsoleColor.White);

        byte[] dummy = new byte[512];

        // 1. Basic Second-Chance
        Output.Write("  [1/4] Second-Chance Eviction : ", ConsoleColor.Gray);
        BlockCacheEngine cache1 = new(capacity, 512, new ClockPolicy());
        for (ulong k = 1; k <= (ulong)capacity; k++) cache1.Put(k, dummy);
        for (ulong k = 1; k <= (ulong)(capacity / 2); k++) cache1.TryGet(k, out _);
        cache1.Put(999, dummy);

        if (cache1.Evictions == 1) Output.WriteLine("PASSED", ConsoleColor.Green);
        else Output.WriteLine("FAILED", ConsoleColor.Red);

        // 2. Sequential Scan
        Output.Write("  [2/4] Sequential Scan        : ", ConsoleColor.Gray);
        BlockCacheEngine scanCache = new(capacity, 512, new ClockPolicy());
        int totalScan = capacity * 4;
        for (ulong k = 1; k <= (ulong)totalScan; k++)
        {
            if (!scanCache.TryGet(k, out _)) scanCache.Put(k, dummy);
        }
        Output.Write($"{scanCache.HitRatio:F1}%", ConsoleColor.Yellow);
        Output.WriteLine($" (Hits: {scanCache.Hits}, Evictions: {scanCache.Evictions})", ConsoleColor.DarkGray);

        // 3. Hotspot 80/20
        Output.Write("  [3/4] Hotspot Access (80/20) : ", ConsoleColor.Gray);
        BlockCacheEngine hotCache = new(capacity, 512, new ClockPolicy());
        for (ulong k = 1; k <= (ulong)capacity; k++) hotCache.Put(k, dummy);
        hotCache.ResetStats();

        Random rnd = new(42);
        ulong hotCount = (ulong)Math.Max(1, capacity / 3);
        for (int req = 0; req < 200; req++)
        {
            ulong k = (rnd.Next(100) < 80) ? (ulong)rnd.Next(1, (int)hotCount + 1) : (ulong)rnd.Next((int)hotCount + 1, capacity * 3);
            if (!hotCache.TryGet(k, out _)) hotCache.Put(k, dummy);
        }
        Output.Write($"{hotCache.HitRatio:F1}%", ConsoleColor.Green);
        Output.WriteLine($" (Hits: {hotCache.Hits}, Evictions: {hotCache.Evictions})", ConsoleColor.DarkGray);

        // 4. Looping Access
        Output.Write("  [4/4] Looping Access (> Cap) : ", ConsoleColor.Gray);
        BlockCacheEngine loopCache = new(capacity, 512, new ClockPolicy());
        int loopSize = capacity + 2;
        for (int i = 0; i < 100; i++)
        {
            ulong k = (ulong)(i % loopSize) + 1;
            if (!loopCache.TryGet(k, out _)) loopCache.Put(k, dummy);
        }
        Output.Write($"{loopCache.HitRatio:F1}%", ConsoleColor.Red);
        Output.WriteLine($" (Hits: {loopCache.Hits}, Evictions: {loopCache.Evictions})", ConsoleColor.DarkGray);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: cache [OPTION]...", ConsoleColor.White);
        Output.WriteLine("Inspect system VFS block cache and run algorithm benchmarks.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -s, --stats           display live cache statistics", ConsoleColor.Gray);
        Output.WriteLine("  -l, --latency <FILE>  measure real disk vs RAM read latency (e.g. 100 iters)", ConsoleColor.Gray);
        Output.WriteLine("  -v, --verbose         toggle live [CACHE HIT] / [CACHE MISS] console traces", ConsoleColor.Gray);
        Output.WriteLine("  --on / --off          enable or disable VFS block caching dynamically", ConsoleColor.Gray);
        Output.WriteLine("  -b, --bench           run benchmark workload tests", ConsoleColor.Gray);
        Output.WriteLine("  --fill [N]            fill cache with N dummy test blocks (default: 8)", ConsoleColor.Gray);
        Output.WriteLine("  -f, --flush           flush system block cache", ConsoleColor.Gray);
        Output.WriteLine("  -h, --help            display this help and exit", ConsoleColor.Gray);
    }
}
