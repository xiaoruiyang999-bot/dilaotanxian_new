/// <summary>
/// 技能类型（v0.7.4 技能框架，计划书 §6.1）。
/// MeleeAoE：以自身为中心的圆形范围打击（旋风斩占位 / 武器技能）。
/// Buff：给自身挂 Buff（v0.7.5 屹立不倒/强力一击，数值在 SkillData 的 Buff 区字段）。
/// DashExecute：冲刺斩杀（v0.7.5 二期裸绞）；BurnLife：燃命大招（清 buff + 免疫 + 分支联动强化）。
/// 扩展位预留：新增类型在此追加枚举值，SkillExecutor.Execute 按类型分发。
/// 序列化值与枚举顺序绑定，新增只能追加，不要重排。
/// </summary>
public enum SkillType
{
    MeleeAoE = 0,   // 自身中心圆形 AOE
    Buff = 1,       // 自身 Buff（持续修饰四通道，可走 BuffManager 虚弱链）
    DashExecute = 2, // 冲刺斩杀（v0.7.5 二期裸绞：冲刺位移 + 阈值斩杀/真伤，数值在 SkillData 裸绞区）
    BurnLife = 3,   // 燃命（v0.7.5 二期大招：清 buff + 免疫窗口 + 按小技能分支联动强化下一发，数值在 SkillData 燃命区）
}
