/// <summary>
/// 技能类型（v0.7.4 技能框架，计划书 §6.1）。
/// MeleeAoE：以自身为中心的圆形范围打击（本版唯一实现，旋风斩占位）。
/// 扩展位预留：新增类型在此追加枚举值，SkillExecutor.Execute 按类型分发。
/// 序列化值与枚举顺序绑定，新增只能追加，不要重排。
/// </summary>
public enum SkillType
{
    MeleeAoE = 0,   // 自身中心圆形 AOE
}
