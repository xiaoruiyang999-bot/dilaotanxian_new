using UnityEngine;

/// <summary>可复用的敌人精英修饰数据；只改变基础属性与表现，不复制 AI/Combat。</summary>
[CreateAssetMenu(menuName = "Combat/Enemy Affix Config", fileName = "EnemyAffixConfig")]
public class EnemyAffixConfig : ScriptableObject
{
    public string displayName = "Affix";
    [Min(0.1f)] public float healthMultiplier = 1f;
    [Min(0.1f)] public float moveSpeedMultiplier = 1f;
    [Min(0.1f)] public float scaleMultiplier = 1f;
    public Color tint = Color.white;

    private void OnValidate()
    {
        healthMultiplier = Mathf.Max(0.1f, healthMultiplier);
        moveSpeedMultiplier = Mathf.Max(0.1f, moveSpeedMultiplier);
        scaleMultiplier = Mathf.Max(0.1f, scaleMultiplier);
    }
}
