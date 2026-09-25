// IBlockCachePolicy.cs — Modular Cache Eviction Policy Strategy Interface
namespace Novellium.IO.Cache;

public interface IBlockCachePolicy
{
    string Name { get; }
    void Init(int capacity);
    void OnAccess(int slotIdx);
    void OnInsert(int slotIdx);
    int SelectEvictionSlot();
    void OnEvict(int slotIdx);
    void Reset();
}
