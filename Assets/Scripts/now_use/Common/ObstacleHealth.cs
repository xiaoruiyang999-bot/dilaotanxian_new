using UnityEngine;

/// <summary>
/// 可破坏障碍物生命（v0.5.2）。实现 IDamageable，直接接入现有 WeaponHitbox 判定链（零新判定代码）。
/// HP 语义 = 需要砍的刀数：每次命中固定扣 1，不看伤害数值（SpawnTable 条目 hp ≈ 刀数）。
/// 只持有数据与事件，不做任何表现——表现由 DestructibleObstacle 监听事件实现。
/// OnDestroyed 是掉落物系统的挂点（v0.5.3+ 接入）。
/// </summary>
public class ObstacleHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHp = 3;

    public int CurrentHp { get; private set; }
    public int MaxHp => maxHp;
    public bool IsDestroyed { get; private set; }

    public System.Action<int, int> OnHpChanged;  // (current, max)
    public System.Action OnTakeDamage;           // 受击（闪白表现监听）
    public System.Action OnDestroyed;            // 破坏（销毁表现 / 未来掉落挂点）

    private void Awake()
    {
        CurrentHp = maxHp;
    }

    /// <summary>SpawnTable 条目注入血量（≈刀数）。生成时由 ObstacleSpawner 调用。</summary>
    public void Init(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        CurrentHp = maxHp;
        IsDestroyed = false;
    }

    /// <summary>实现 IDamageable。每次命中固定 -1（刀数语义）。</summary>
    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;

        CurrentHp = Mathf.Max(CurrentHp - 1, 0);
        OnHpChanged?.Invoke(CurrentHp, maxHp);
        OnTakeDamage?.Invoke();

        if (CurrentHp <= 0)
        {
            IsDestroyed = true;
            OnDestroyed?.Invoke();
        }
    }
}
