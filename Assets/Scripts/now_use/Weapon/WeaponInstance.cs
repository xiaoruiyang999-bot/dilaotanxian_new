/// <summary>
/// 武器运行时实例（v0.6.2 阶段 A）：WeaponData + 动态状态（弹夹/换弹/蓄力计时）。
/// 纯 C# 类，由 PlayerWeaponHolder 持有；v0.6.3 弹夹/换弹/蓄力逻辑在此扩展。
/// </summary>
public class WeaponInstance
{
    /// <summary>配置数据（SO 引用，只读）。</summary>
    public WeaponData Data { get; }

    /// <summary>当前弹夹余量（clipSize = 0 表示无弹夹，恒为 0）。</summary>
    public int CurrentClip { get; private set; }

    /// <summary>换弹剩余时间（> 0 表示换弹中）。由持有方每帧递减。</summary>
    public float ReloadTimer { get; set; }

    /// <summary>蓄力计时（v0.6.3 蓄力系统使用）。</summary>
    public float ChargeTimer { get; set; }

    public bool IsReloading => ReloadTimer > 0f;

    public WeaponInstance(WeaponData data)
    {
        Data = data;
        CurrentClip = data != null ? data.ClipSize : 0;
        ReloadTimer = 0f;
        ChargeTimer = 0f;
    }

    /// <summary>消耗一发弹药；无弹夹（clipSize = 0）恒成功。</summary>
    public bool TryConsumeAmmo()
    {
        if (Data == null || Data.ClipSize <= 0) return true;
        if (CurrentClip <= 0) return false;
        CurrentClip--;
        return true;
    }

    /// <summary>装填满弹夹并清除换弹计时。</summary>
    public void FinishReload()
    {
        if (Data != null) CurrentClip = Data.ClipSize;
        ReloadTimer = 0f;
    }
}
