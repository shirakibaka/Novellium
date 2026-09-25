// ClockPolicy.cs — Clock (Second-Chance) Eviction Policy Strategy
using System;

namespace Novellium.IO.Cache;

public class ClockPolicy : IBlockCachePolicy
{
    private bool[] RefBits = Array.Empty<bool>();
    private int Hand;
    private int Capacity;

    public string Name => "Clock (Second-Chance)";

    public void Init(int capacity)
    {
        Capacity = capacity;
        RefBits = new bool[capacity];
        Hand = 0;
    }

    public void OnAccess(int slotIdx)
    {
        if (slotIdx >= 0 && slotIdx < Capacity)
        {
            RefBits[slotIdx] = true;
        }
    }

    public void OnInsert(int slotIdx)
    {
        if (slotIdx >= 0 && slotIdx < Capacity)
        {
            RefBits[slotIdx] = false;
        }
    }

    public int SelectEvictionSlot()
    {
        if (Capacity <= 0) return 0;

        while (true)
        {
            if (RefBits[Hand])
            {
                RefBits[Hand] = false;
                Hand = (Hand + 1) % Capacity;
            }
            else
            {
                int victim = Hand;
                Hand = (Hand + 1) % Capacity;
                return victim;
            }
        }
    }

    public void OnEvict(int slotIdx)
    {
        if (slotIdx >= 0 && slotIdx < Capacity)
        {
            RefBits[slotIdx] = false;
        }
    }

    public void Reset()
    {
        Array.Clear(RefBits, 0, RefBits.Length);
        Hand = 0;
    }
}
