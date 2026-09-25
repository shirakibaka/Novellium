// VfsCacheEngine.cs — High-level VFS multi-block page cache integration wrapper
using System;
using System.IO;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.Commands;
using Novellium.Services;

namespace Novellium.IO.Cache;

public static class VfsCacheEngine
{
    private static readonly BlockCacheEngine SystemEngine = new(capacity: 32768, blockSize: 4096, new ClockPolicy());

    public static bool Enabled { get; set; } = true;
    public static bool Verbose { get; set; } = false;
    public static BlockCacheEngine Engine => SystemEngine;

    public static ulong ComputeKey(string path, long blockIdx = 0)
    {
        ulong hash = 14695981039346656037UL;
        string normalized = CManager.NormalizePath(path).ToLowerInvariant();
        for (int i = 0; i < normalized.Length; i++)
        {
            hash ^= normalized[i];
            hash *= 1099511628211UL;
        }
        hash ^= (ulong)blockIdx;
        hash *= 1099511628211UL;
        return hash;
    }

    public static bool TryReadFile(string path, out byte[]? data)
    {
        if (!Enabled)
        {
            data = null;
            return false;
        }

        if (!VfsManager.TryStat(path, out VfsStat st) || st.IsDirectory || st.Size == 0)
        {
            data = null;
            return false;
        }

        int blockSize = SystemEngine.BlockSize; // 4096 bytes
        long totalSize = (long)st.Size;
        int numBlocks = (int)((totalSize + blockSize - 1) / blockSize);

        byte[] fullBuffer = new byte[totalSize];
        bool allBlocksCached = true;

        // Check if all 4KB pages of this file are present in cache
        for (int b = 0; b < numBlocks; b++)
        {
            ulong pageKey = ComputeKey(path, b);
            if (SystemEngine.TryGet(pageKey, out var pageData) && pageData != null)
            {
                int copyLen = (int)Math.Min((long)blockSize, totalSize - (b * (long)blockSize));
                Array.Copy(pageData, 0, fullBuffer, b * blockSize, copyLen);
            }
            else
            {
                allBlocksCached = false;
                break;
            }
        }

        if (allBlocksCached)
        {
            Syslogd.Log(LogLevel.Debug, "vfs", $"Cache HIT: Served '{path}' ({totalSize} B, {numBlocks} pages) directly from RAM pool");
            if (Verbose)
            {
                Output.WriteTag("CACHE HIT", ConsoleColor.Green, $"Served '{path}' ({totalSize} B, {numBlocks} pages) directly from RAM pool");
            }
            data = fullBuffer;
            return true;
        }

        // Cache MISS — Read from Ext2 disk and populate 4KB page slots
        if (!VfsManager.TryOpenFile(path, out var handle) || handle == null)
        {
            data = null;
            return false;
        }

        using (handle)
        {
            try
            {
                byte[] rawDiskBuffer = new byte[totalSize];
                long read = handle.Read(rawDiskBuffer);
                if (read > 0)
                {
                    // Partition file into 4KB pages and cache each page
                    for (int b = 0; b < numBlocks; b++)
                    {
                        ulong pageKey = ComputeKey(path, b);
                        long offset = b * (long)blockSize;
                        int chunkSize = (int)Math.Min((long)blockSize, read - offset);
                        if (chunkSize <= 0) break;

                        byte[] pageChunk = new byte[chunkSize];
                        Array.Copy(rawDiskBuffer, offset, pageChunk, 0, chunkSize);
                        SystemEngine.Put(pageKey, pageChunk);
                    }

                    data = rawDiskBuffer;
                    Syslogd.Log(LogLevel.Debug, "vfs", $"Cache MISS: Read '{path}' ({read} B, {numBlocks} pages) from Ext2 disk into RAM pool");
                    if (Verbose)
                    {
                        Output.WriteTag("CACHE MISS", ConsoleColor.Yellow, $"Read '{path}' ({read} B, {numBlocks} pages) from Ext2 disk into RAM pool");
                    }
                    return true;
                }
            }
            catch
            {
                data = null;
            }
        }

        data = null;
        return false;
    }

    public static void Invalidate(string path)
    {
        if (!VfsManager.TryStat(path, out VfsStat st) || st.IsDirectory)
        {
            ulong key0 = ComputeKey(path, 0);
            SystemEngine.Invalidate(key0);
            return;
        }

        int blockSize = SystemEngine.BlockSize;
        long totalSize = (long)st.Size;
        int numBlocks = (int)((totalSize + blockSize - 1) / blockSize);
        for (int b = 0; b < Math.Max(1, numBlocks); b++)
        {
            ulong pageKey = ComputeKey(path, b);
            SystemEngine.Invalidate(pageKey);
        }
    }

    public static void Clear()
    {
        SystemEngine.Clear();
    }
}
