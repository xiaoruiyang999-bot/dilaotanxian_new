#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 狼人序列帧烘焙工具（v1.2.2，GDD §22.6 新动画主链）：
/// Resources/Art/Characters/Werewolf/{组}_L|R\NNN.png → Assets/Resources/Animation/Werewolf/ 下
/// .anim 资产（等间隔逐帧 sprite 曲线，12fps 循环）+ Werewolf.controller（状态机资产）。
/// sprite 引用曲线只能编辑器构建（运行时 API 限制）——烘焙入库后运行时 Resources.Load 消费，
/// 符合"Editor 便利代码打包前资源化"红线。素材更新后重跑菜单即可再烘焙。
/// 状态合同：Idle/Walk/BeastIdle/BeastWalk × _L/_R；参数 MoveX/Speed/IsTransformed。
/// 攻击帧未产出：Attack 状态待素材到位后扩展（AttackDefinition.animationState 对接）。
/// </summary>
public static class WerewolfAnimationBaker
{
    private const string ArtRoot = "Assets/Resources/Art/Characters/Werewolf";
    private const string OutDir = "Assets/Resources/Animation/Werewolf";
    private const string ControllerPath = OutDir + "/Werewolf.controller";
    private const int Fps = 12;

    [MenuItem("Tools/Werewolf/Bake Animations")]
    public static void BakeAll()
    {
        Directory.CreateDirectory(OutDir);
        var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();

        string[] groups = { "Idle", "Walk", "BeastIdle", "BeastWalk", "Transform" };
        foreach (string group in groups)
            foreach (string side in new[] { "_L", "_R" })
            {
                AnimationClip clip = BakeClip($"{group}{side}");
                if (clip != null) clips[$"{group}{side}"] = clip;
            }

        if (clips.Count == 0)
        {
            Debug.LogWarning("[WerewolfBaker] 未找到任何帧组，取消烘焙。");
            return;
        }
        BakeController(clips);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WerewolfBaker] 烘焙完成：{clips.Count} 个 Clip + Controller → {OutDir}");
    }

    /// <summary>帧目录 → .anim（等间隔 sprite 曲线）。目录缺失返回 null。</summary>
    private static AnimationClip BakeClip(string groupName)
    {
        string dir = $"{ArtRoot}/{groupName}";
        if (!Directory.Exists(dir)) return null;

        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (string file in Directory.GetFiles(dir, "*.png"))
        {
            string assetPath = file.Replace('\\', '/');
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) sprites.Add(sprite);
        }
        if (sprites.Count == 0) return null;
        sprites.Sort((a, b) => NaturalCompare(a.name, b.name));

        string clipPath = $"{OutDir}/{groupName}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = groupName };
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            BuildKeys(sprites));

        clip.wrapMode = groupName.StartsWith("Transform") ? WrapMode.Once : WrapMode.Loop;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static ObjectReferenceKeyframe[] BuildKeys(System.Collections.Generic.List<Sprite> sprites)
    {
        var keys = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keys[i].time = (float)i / Fps;
            keys[i].value = sprites[i];
        }
        return keys;
    }

    /// <summary>构建/更新 Werewolf.controller：全状态 + 条件转换。</summary>
    private static void BakeController(System.Collections.Generic.Dictionary<string, AnimationClip> clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }
        var machine = controller.layers[0].stateMachine;

        // 参数合同（GDD §22.6：少量稳定参数）
        EnsureParam(controller, "MoveX", AnimatorControllerParameterType.Float);
        EnsureParam(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParam(controller, "IsTransformed", AnimatorControllerParameterType.Bool);

        var byName = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var kv in clips)
        {
            AnimatorState st = FindOrAddState(machine, kv.Key);
            st.motion = kv.Value;
            byName[kv.Key] = st;
        }
        if (byName.TryGetValue("Idle_R", out var entry)) machine.defaultState = entry;

        foreach (string side in new[] { "_L", "_R" })
        {
            Link(byName, $"Idle{side}", $"Walk{side}", "Speed", AnimatorConditionMode.Greater, 0.1f,
                AnimatorConditionMode.Less, 0.1f);
            Link(byName, $"BeastIdle{side}", $"BeastWalk{side}", "Speed", AnimatorConditionMode.Greater, 0.1f,
                AnimatorConditionMode.Less, 0.1f);
            Link(byName, $"Idle{side}", $"BeastIdle{side}", "IsTransformed",
                AnimatorConditionMode.If, 0f, AnimatorConditionMode.IfNot, 0f);
            Link(byName, $"Walk{side}", $"BeastWalk{side}", "IsTransformed",
                AnimatorConditionMode.If, 0f, AnimatorConditionMode.IfNot, 0f);
        }
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (System.Array.FindIndex(controller.parameters, p => p.name == name) < 0)
            controller.AddParameter(name, type);
    }

    private static AnimatorState FindOrAddState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state.name == name) return child.state;
        return machine.AddState(name);
    }

    /// <summary>双向条件连接：a→b（modeA/param/thresholdA）与 b→a（modeB 同参数）。</summary>
    private static void Link(System.Collections.Generic.Dictionary<string, AnimatorState> byName,
        string a, string b, string param,
        AnimatorConditionMode modeA, float thresholdA, AnimatorConditionMode modeB, float thresholdB)
    {
        if (!byName.TryGetValue(a, out var sa) || !byName.TryGetValue(b, out var sb)) return;
        var forward = sa.AddTransition(sb);
        forward.AddCondition(modeA, thresholdA, param);
        var back = sb.AddTransition(sa);
        back.AddCondition(modeB, thresholdB, param);
    }

    private static int NaturalCompare(string a, string b)
    {
        int ia = a.Length - 1, ib = b.Length - 1;
        while (ia >= 0 && char.IsDigit(a[ia])) ia--;
        while (ib >= 0 && char.IsDigit(b[ib])) ib--;
        bool na = ia < a.Length - 1, nb = ib < b.Length - 1;
        if (na && nb && a.Substring(0, ia + 1) == b.Substring(0, ib + 1))
            return int.Parse(a.Substring(ia + 1)).CompareTo(int.Parse(b.Substring(ib + 1)));
        return string.CompareOrdinal(a, b);
    }
}
#endif
