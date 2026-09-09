using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v1.2.2 新数据边界门禁：狼人职业角色定义与爪击攻击资产可从 Resources 加载且字段合法
///（GDD §8/§22.6 合同；静态配置=SO，运行时按路径镜像加载）。资产缺失/字段非法在此红灯，
/// 防止灰盒期手改资产把边界改坏。
/// </summary>
public class PlayableCharacterDefinitionTests
{
    private const string CharacterPath = "Characters/Character_Werewolf";
    private const string AttackPath = "Characters/Attack_WerewolfClaws";

    [Test]
    public void WerewolfDefinition_LoadsAndIsValid()
    {
        var def = Resources.Load<PlayableCharacterDefinition>(CharacterPath);
        Assert.NotNull(def, $"缺少 {CharacterPath}（SO 资产未入库或脚本绑定断链）");

        Assert.AreEqual("Werewolf", def.characterId);
        Assert.IsNotEmpty(def.displayName);
        Assert.GreaterOrEqual(def.maxHp, 1f);
        Assert.Greater(def.moveSpeed, 0.1f);
        Assert.IsNotEmpty(def.artFolder, "动画目录合同不得为空");
        Assert.IsNotEmpty(def.idleFolder);
        Assert.IsNotEmpty(def.walkFolder);
        Assert.Greater(def.beastResourceMax, 0f, "狼人必有兽性资源（GDD §8.2）");
        Assert.IsNotNull(def.basicAttack, "必须挂普攻模组（AttackDefinition）");
    }

    [Test]
    public void WerewolfClaws_ValidComboSteps()
    {
        var attack = Resources.Load<AttackDefinition>(AttackPath);
        Assert.NotNull(attack, $"缺少 {AttackPath}");
        Assert.That(attack.steps, Is.Not.Empty, "连段模组不得为空");
        Assert.That(attack.steps.Length, Is.InRange(2, 3), "MVP 固定 2~3 段（GDD §7.3）");

        for (int i = 0; i < attack.steps.Length; i++)
        {
            var s = attack.steps[i];
            Assert.Greater(s.active, 0f, $"段{i} 有效判定时长必须为正");
            Assert.Greater(s.reach, 0.2f, $"段{i} 攻击距离非法");
            Assert.Greater(s.laneWidth, 0.3f, $"段{i} 纵深容错非法");
            Assert.Greater(s.damageMultiplier, 0f, $"段{i} 伤害倍率非法");
            Assert.That(s.recoveryCancelRatio, Is.InRange(0f, 1f), $"段{i} 取消点越界");
            Assert.Greater(s.comboWindow, 0f, $"段{i} 连段窗口非法");
        }
    }

    [Test]
    public void BakedWerewolfController_LoadsWithParamContract()
    {
        var controller = Resources.Load<RuntimeAnimatorController>("Animation/Werewolf/Werewolf");
        Assert.NotNull(controller, "烘焙资产缺失——运行 Tools/Werewolf/Bake Animations");
    }
}
