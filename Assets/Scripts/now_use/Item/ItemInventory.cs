using UnityEngine;

/// <summary>
/// 道具堆叠（v0.7.2）：同类消耗品 + 数量，count 无叠加上限（UI 角标超 99 显示 99+）。
/// </summary>
[System.Serializable]
public class ItemStack
{
    public ConsumableData Data;
    public int Count;

    public ItemStack(ConsumableData data, int count)
    {
        Data = data;
        Count = count;
    }
}

/// <summary>
/// 玩家道具背包（v0.7.2，纯数据 + 事件）：
/// 道具栏激活位 1 格 + 背包 3 格，同类叠加无上限。
/// Add 分流：道具栏同类叠加 → 栏空入栏 → 背包同类叠加 → 背包空位 → 满返回 false；
/// UseActive 按 effectType 结算效果（v0.7.3：Health.Heal / PlayerStats.ModifyArmor / AddMana）
/// 后扣数、清零出槽；SwapWithBackpack 道具栏与背包格互换。
/// 由 PlayerController.Awake 运行时挂载（与 PlayerInteractor 同模式，不改 prefab YAML）。
/// </summary>
public class ItemInventory : MonoBehaviour
{
    /// <summary>背包格数（v0.7.2 固定 3 格）。</summary>
    public const int BackpackSize = 3;

    private Health cachedHealth;     // 玩家组件缓存（本组件挂在 Player 上）
    private PlayerStats cachedStats;

    /// <summary>道具栏激活位（无道具时为 null）。</summary>
    public ItemStack ActiveSlot { get; private set; }

    /// <summary>背包 3 格（空位元素为 null）。</summary>
    public ItemStack[] Backpack { get; } = new ItemStack[BackpackSize];

    /// <summary>任何槽位变动（拾取/使用/互换）后触发，供 SlotBarUI 刷新。</summary>
    public event System.Action OnChanged;

    /// <summary>
    /// 拾取分流（计划书 §一.2）：道具栏同类叠加 → 栏空入栏 → 背包同类叠加 →
    /// 背包空位 → 全满返回 false（调用方保留地上拾取物并提示"背包已满"）。
    /// </summary>
    public bool Add(ConsumableData data)
    {
        if (data == null) return false;

        // 1. 道具栏同类叠加
        if (ActiveSlot != null && ActiveSlot.Data == data)
        {
            ActiveSlot.Count++;
            OnChanged?.Invoke();
            return true;
        }

        // 2. 道具栏空 → 入栏
        if (ActiveSlot == null)
        {
            ActiveSlot = new ItemStack(data, 1);
            OnChanged?.Invoke();
            return true;
        }

        // 3. 背包同类叠加
        foreach (ItemStack slot in Backpack)
        {
            if (slot != null && slot.Data == data)
            {
                slot.Count++;
                OnChanged?.Invoke();
                return true;
            }
        }

        // 4. 背包空位
        for (int i = 0; i < Backpack.Length; i++)
        {
            if (Backpack[i] == null)
            {
                Backpack[i] = new ItemStack(data, 1);
                OnChanged?.Invoke();
                return true;
            }
        }

        // 5. 满
        return false;
    }

    /// <summary>
    /// 使用道具栏激活项（UseItem 键）：先按 effectType 结算效果，数量 −1，减到 0 槽位清空。
    /// 未装备消耗品时无副作用；对应属性已满也照常消耗（v0.7.3 规则：简单优先，不拦截）。
    /// </summary>
    public void UseActive()
    {
        if (ActiveSlot == null || ActiveSlot.Data == null || ActiveSlot.Count <= 0) return;

        ApplyEffect(ActiveSlot.Data);

        ActiveSlot.Count--;
        if (ActiveSlot.Count <= 0)
            ActiveSlot = null;
        OnChanged?.Invoke();
    }

    /// <summary>按消耗品效果类型结算（v0.7.3）：HP → Health.Heal；Armor → PlayerStats.ModifyArmor；Mana → PlayerStats.AddMana。</summary>
    private void ApplyEffect(ConsumableData data)
    {
        switch (data.EffectType)
        {
            case ConsumableEffectType.HP:
                if (cachedHealth == null) cachedHealth = GetComponent<Health>();
                if (cachedHealth != null) cachedHealth.Heal(data.Value);
                break;
            case ConsumableEffectType.Armor:
                if (cachedStats == null) cachedStats = GetComponent<PlayerStats>();
                if (cachedStats != null) cachedStats.ModifyArmor(data.Value);
                break;
            case ConsumableEffectType.Mana:
                if (cachedStats == null) cachedStats = GetComponent<PlayerStats>();
                if (cachedStats != null) cachedStats.AddMana(data.Value);
                break;
        }
    }

    /// <summary>道具栏与背包格互换（背包格鼠标点击调用；空格互换即"取出/放入"）。</summary>
    public void SwapWithBackpack(int index)
    {
        if (index < 0 || index >= Backpack.Length) return;

        ItemStack tmp = ActiveSlot;
        ActiveSlot = Backpack[index];
        Backpack[index] = tmp;
        OnChanged?.Invoke();
    }
}
