using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 合并门禁（v1.0.4，审查报告 §六最小测试集第 4 项）：
/// 正式场景与全部 Prefab 不允许存在 Missing Script。
/// 历史事故：v0.7.5 合并删除旧 UI 脚本后场景仍保留序列化引用，形成 5 个 Missing Script 断链（v1.0.3 报告 §二）。
/// </summary>
public class MissingScriptScanTests
{
    private static readonly string[] OfficialScenes =
    {
        "Assets/Scenes/v0_7_PrepRoom.unity",
        "Assets/Scenes/v0_7_ClassWeapon.unity",
        "Assets/Scenes/v0_5_Dungeon.unity",   // 旧直连测试场景，一并观察
    };

    private string originalScenePath;

    [SetUp]
    public void RememberCurrentScene()
    {
        originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }

    [TearDown]
    public void RestoreCurrentScene()
    {
        if (!string.IsNullOrEmpty(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
    }

    [Test]
    public void OfficialScenes_HaveNoMissingScripts()
    {
        var failures = new List<string>();
        foreach (string path in OfficialScenes)
        {
            Assert.True(System.IO.File.Exists(path), $"场景文件不存在：{path}（门禁清单过期？）");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (GameObject go in scene.GetRootGameObjects())
                CollectMissing(go.transform, path, failures);
        }
        Assert.That(failures, Is.Empty, "Missing Script 断链：\n" + string.Join("\n", failures));
    }

    [Test]
    public void AllPrefabs_HaveNoMissingScripts()
    {
        var failures = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        Assert.That(guids, Is.Not.Empty, "Assets/Prefabs 下未找到任何 Prefab——清单或路径有误");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            CollectMissing(root.transform, path, failures);
        }
        Assert.That(failures, Is.Empty, "Missing Script 断链：\n" + string.Join("\n", failures));
    }

    private static void CollectMissing(Transform t, string source, List<string> failures)
    {
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        if (missing > 0)
            failures.Add($"{source} → {GetPath(t)}（{missing} 个丢失组件）");

        for (int i = 0; i < t.childCount; i++)
            CollectMissing(t.GetChild(i), source, failures);
    }

    private static string GetPath(Transform t)
    {
        var stack = new List<string>();
        while (t != null) { stack.Insert(0, t.name); t = t.parent; }
        return string.Join("/", stack);
    }
}
