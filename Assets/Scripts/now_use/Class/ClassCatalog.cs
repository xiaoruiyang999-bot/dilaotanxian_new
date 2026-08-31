using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业资产目录（v0.6.2 阶段 B）：运行时获取三份 ClassData。
/// 编辑器下用 AssetDatabase 按路径加载（场景无 Inspector 接线点，编辑器运行期间不改场景 YAML）；
/// 打包构建需把三份资产移入 Resources/Class/ 走 Resources.Load（见 v0.6.2操作.md 交接说明）。
/// </summary>
public static class ClassCatalog
{
    private static ClassData[] classes;

    /// <summary>三职业（Warrior/Archer/Mage 顺序固定）。加载失败元素为 null。</summary>
    public static IReadOnlyList<ClassData> All
    {
        get
        {
            if (classes == null) Load();
            return classes;
        }
    }

    private static void Load()
    {
        classes = new ClassData[3];
#if UNITY_EDITOR
        classes[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<ClassData>("Assets/Data/Class/Class_Warrior.asset");
        classes[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<ClassData>("Assets/Data/Class/Class_Archer.asset");
        classes[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<ClassData>("Assets/Data/Class/Class_Mage.asset");
#else
        classes[0] = Resources.Load<ClassData>("Class/Class_Warrior");
        classes[1] = Resources.Load<ClassData>("Class/Class_Archer");
        classes[2] = Resources.Load<ClassData>("Class/Class_Mage");
#endif
        if (classes[0] == null || classes[1] == null || classes[2] == null)
            Debug.LogError("[Class] ClassData 加载失败（编辑器应走 AssetDatabase；构建需 Resources/Class/ 资产）。");
    }
}
