// ClockCache.cs — Standalone Clock (Second-Chance) Page/Block Cache Engine
using System;
using System.Collections.Generic;

namespace Novellium.IO;

public class ClockCache
{
    public struct Slot
    {
        public ulong Key;
        public byte[] Data;
        public bool RefBit;
        public bool Valid;
    }

    private readonly Slot[] Slots;
    private readonly Dictionary<ulong, int> IndexMap;
    private int Hand;

    public int Capacity { get; }
    public int Count { get; private set; }
    public long Hits { get; private set; }
    public long Misses { get; private set; }
    public long Evictions { get; private set; }

    public ClockCache(int capacity)
    {
        Capacity = Math.Max(1, capacity);
        Slots = new Slot[Capacity];
        IndexMap = new Dictionary<ulong, int>(Capacity);
        Hand = 0;
        Count = 0;
    }

    public double HitRatio => (Hits + Misses) > 0 ? ((double)Hits / (Hits + Misses)) * 100.0 : 0.0;

    public bool TryGet(ulong key, out byte[]? data)
    {
        if (IndexMap.TryGetValue(key, out int idx))
        {
            Slots[idx].RefBit = true;
            Hits++;
            data = Slots[idx].Data;
            return true;
        }

        Misses++;
        data = null;
        return false;
    }

    public void Put(ulong key, byte[] data)
    {
        if (IndexMap.TryGetValue(key, out int existingIdx))
        {
            Slots[existingIdx].Data = data;
            Slots[existingIdx].RefBit = true;
            return;
        }

        if (Count < Capacity)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!Slots[i].Valid)
                {
                    Slots[i].Key = key;
                    Slots[i].Data = data;
                    Slots[i].RefBit = true;
                    Slots[i].Valid = true;
                    IndexMap[key] = i;
                    Count++;
                    return;
                }
            }
        }

        // Clock Eviction Sweep (Second Chance)
        while (true)
        {
            if (Slots[Hand].RefBit)
            {
                Slots[Hand].RefBit = false;
                Hand = (Hand + 1) % Capacity;
            }
            else
            {
                // Evict slot at Hand
                ulong oldKey = Slots[Hand].Key;
                IndexMap.Remove(oldKey);

                Slots[Hand].Key = key;
                Slots[Hand].Data = data;
                Slots[Hand].RefBit = true;
                Slots[Hand].Valid = true;
                IndexMap[key] = Hand;

                Evictions++;
                Hand = (Hand + 1) % Capacity;
                break;
            }
        }
    }

    public bool ContainsKey(ulong key) => IndexMap.ContainsKey(key);

    public bool GetRefBit(ulong key)
    {
        if (IndexMap.TryGetValue(key, out int idx)) return Slots[idx].RefBit;
        return false;
    }

    public void Clear()
    {
        IndexMap.Clear();
        for (int i = 0; i < Capacity; i++)
        {
            Slots[i] = default;
        }
        Hand = 0;
        Count = 0;
    }

    public void ResetStats()
    {
        Hits = 0;
        Misses = 0;
        Evictions = 0;
    }
}
