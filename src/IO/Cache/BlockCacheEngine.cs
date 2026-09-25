// BlockCacheEngine.cs — High-Performance Zero-Alloc Block Cache Engine
using System;
using System.Collections.Generic;

namespace Novellium.IO.Cache;

public class BlockCacheEngine
{
    public struct Slot
    {
        public ulong Key;
        public bool Valid;
        public bool Dirty;
    }

    private readonly object Lock = new();
    private readonly Slot[] Slots;
    private readonly byte[][] BufferPool;
    private readonly Dictionary<ulong, int> IndexMap;

    public int Capacity { get; }
    public int BlockSize { get; }
    public int Count { get; private set; }
    public long Hits { get; private set; }
    public long Misses { get; private set; }
    public long Evictions { get; private set; }
    public IBlockCachePolicy ActivePolicy { get; private set; }

    public BlockCacheEngine(int capacity = 16, int blockSize = 512, IBlockCachePolicy? policy = null)
    {
        Capacity = Math.Max(1, capacity);
        BlockSize = Math.Max(64, blockSize);

        Slots = new Slot[Capacity];
        BufferPool = new byte[Capacity][]; // Lazy allocated on demand

        IndexMap = new Dictionary<ulong, int>(Capacity);
        ActivePolicy = policy ?? new ClockPolicy();
        ActivePolicy.Init(Capacity);
    }

    public double HitRatio => (Hits + Misses) > 0 ? ((double)Hits / (Hits + Misses)) * 100.0 : 0.0;

    public long UsedPayloadBytes => (long)Count * BlockSize;
    public long UsedPayloadBits => UsedPayloadBytes * 8L;
    public long TotalCapacityBytes => (long)Capacity * BlockSize;
    public long TotalCapacityBits => TotalCapacityBytes * 8L;
    public long MetadataOverheadBytes => (long)Capacity * 16L;

    public bool ContainsKey(ulong key)
    {
        lock (Lock)
        {
            return IndexMap.TryGetValue(key, out int idx) && Slots[idx].Valid;
        }
    }

    public void SetPolicy(IBlockCachePolicy newPolicy)
    {
        lock (Lock)
        {
            ActivePolicy = newPolicy;
            ActivePolicy.Init(Capacity);
            ActivePolicy.Reset();

            for (int i = 0; i < Capacity; i++)
            {
                if (Slots[i].Valid) ActivePolicy.OnInsert(i);
            }
        }
    }

    public bool TryGet(ulong key, out byte[]? data)
    {
        lock (Lock)
        {
            if (IndexMap.TryGetValue(key, out int idx) && Slots[idx].Valid && BufferPool[idx] != null)
            {
                ActivePolicy.OnAccess(idx);
                Hits++;
                data = BufferPool[idx];
                return true;
            }

            Misses++;
            data = null;
            return false;
        }
    }

    public void Put(ulong key, byte[] data, bool isDirty = false)
    {
        lock (Lock)
        {
            if (IndexMap.TryGetValue(key, out int existingIdx) && Slots[existingIdx].Valid)
            {
                if (BufferPool[existingIdx] == null) BufferPool[existingIdx] = new byte[BlockSize];
                int copyLen = Math.Min(data.Length, BlockSize);
                Array.Copy(data, 0, BufferPool[existingIdx], 0, copyLen);
                Slots[existingIdx].Dirty = isDirty;
                ActivePolicy.OnAccess(existingIdx);
                return;
            }

            int slotIdx;
            if (Count < Capacity)
            {
                slotIdx = Count;
                Count++;
            }
            else
            {
                slotIdx = ActivePolicy.SelectEvictionSlot();
                if (Slots[slotIdx].Valid)
                {
                    IndexMap.Remove(Slots[slotIdx].Key);
                    ActivePolicy.OnEvict(slotIdx);
                    Evictions++;
                }
            }

            if (BufferPool[slotIdx] == null) BufferPool[slotIdx] = new byte[BlockSize];
            int len = Math.Min(data.Length, BlockSize);
            Array.Copy(data, 0, BufferPool[slotIdx], 0, len);
            Slots[slotIdx].Key = key;
            Slots[slotIdx].Valid = true;
            Slots[slotIdx].Dirty = isDirty;

            IndexMap[key] = slotIdx;
            ActivePolicy.OnInsert(slotIdx);
        }
    }

    public bool Invalidate(ulong key)
    {
        lock (Lock)
        {
            if (IndexMap.TryGetValue(key, out int idx))
            {
                Slots[idx].Valid = false;
                Slots[idx].Dirty = false;
                IndexMap.Remove(key);
                ActivePolicy.OnEvict(idx);
                return true;
            }
            return false;
        }
    }

    public void Clear()
    {
        lock (Lock)
        {
            IndexMap.Clear();
            for (int i = 0; i < Capacity; i++)
            {
                Slots[i] = default;
            }
            Count = 0;
            ActivePolicy.Reset();
        }
    }

    public void ResetStats()
    {
        lock (Lock)
        {
            Hits = 0;
            Misses = 0;
            Evictions = 0;
        }
    }
}
