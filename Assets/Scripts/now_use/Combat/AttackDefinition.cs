using System;
using UnityEngine;

/// <summary>
/// v1.2 攻击同源数据边界（GDD §7/§22.6，迁移顺序 §26.3-2）：
/// 一次攻击（或连段中的一段）的前摇、有效判定、后摇、方向、判定几何、位移、伤害与反馈
/// 全部由本 SO 定义——预警、动画窗口、Hitbox、踏步与命中特效消费同一份，不得各自另算。
/// 旧链等价物：AttackData(敌人/旧武器) + MeleeComboTable(玩家连段表)。旧链保持兼容不动，
/// 新消费方（狼人 Animator 灰盒起）逐步迁移到本定义。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/AttackDefinition", fileName = "Attack_")]
public class AttackDefinition : ScriptableObject
{
    [Serializable]
    public class Step
    {
        [Tooltip("段显示名（如 爪击一式）")]
        public string displayName;

        [Header("三阶段时长（秒，攻速倍率统一叠除）")]
        [Min(0f)] public float windup = 0.14f;
        [Min(0.02f)] public float active = 0.11f;
        [Min(0f)] public float recovery = 0.15f;

        [Header("判定几何（横向攻击带：X=攻击距离，Y=纵深容错）")]
        [Min(0.2f)] public float reach = 1.6f;
        [Min(0.3f)] public float laneWidth = 0.95f;

        [Header("位移与节奏")]
        [Tooltip("踏步水平冲量（世界单位/秒，持续约 0.12s 线性衰减）")]
        public float stepImpulse = 2.2f;
        [Range(0f, 1f)] public float recoveryCancelRatio = 0.35f;
        [Tooltip("连段接受窗口（秒）：后摇完毕后限时可接下一段")]
        public float comboWindow = 0.30f;

        [Header("数值")]
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("攻击附加固定伤害（叠角色 Attack 之上，对应旧 AttackData.AttackDamage）")]
        public float bonusDamage = 0f;

        [Header("动画（素材到位后消费）")]
        [Tooltip("该段动画状态名（Animator）；空 = 用攻击时长驱动占位状态")]
        public string animationState;
        [Tooltip("有效判定窗口由 Animation Event 驱动时为 true；false = 数据计时（灰盒默认）")]
        public bool windowFromAnimationEvent;
    }

    [Tooltip("连段模组（2~3 段固定，MVP 不做任意连招编辑器）")]
    public Step[] steps = Array.Empty<Step>();

    [Header("反馈（GDD §7.4 层级）")]
    [Tooltip("普通命中停帧（秒）")]
    [Range(0f, 0.12f)] public float hitStop = 0.03f;
    [Tooltip("普通命中震屏强度（世界单位）")]
    [Range(0f, 0.3f)] public float hitShake = 0.05f;
}
