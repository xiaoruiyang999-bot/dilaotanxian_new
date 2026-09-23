#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>编辑器 PlayMode 临时存档路线走查；结果写入项目 Temp，不触碰真实存档。</summary>
public static class V211RouteSmokeRunner
{
    [Serializable]
    private sealed class Report
    {
        public bool passed;
        public string message;
        public int visited;
        public int nodeId;
    }

    private static string tempRoot;
    private static DateTime deadline;
    private static bool active;
    private static bool sceneRequested;

    [MenuItem("Tools/Dungeon/V211 Smoke Run")]
    private static void StartRun()
    {
        if (!EditorApplication.isPlaying || active)
        {
            Debug.LogError("[V211Smoke] 请在空白场景的 PlayMode 中运行一次。");
            return;
        }
        tempRoot = Path.Combine(Path.GetTempPath(), "v211-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        SaveService.EditorStorageRootOverride = tempRoot;
        RunStateCarrier.Ensure().SetPlayableCharacter(PlayableCharacterId.Werewolf);
        // 模拟旧版 ActiveRun：玩家 Prefab 默认 5 点生命、0 护甲曾被误写入快照。
        SaveService.ActiveRunData legacy = SaveService.CreateNewRun(
            20260921, PlayableCharacterId.Werewolf);
        legacy.currentHp = 5;
        legacy.currentArmor = 0;
        legacy.currentMana = 0f;
        legacy.resourcesInitialized = false;
        SaveService.SaveRun(legacy);
        deadline = DateTime.UtcNow.AddSeconds(60);
        active = true;
        sceneRequested = false;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!active) return;
        if (!sceneRequested)
        {
            sceneRequested = true;
            SceneManager.LoadScene("v0_7_ClassWeapon");
            return;
        }
        if (DateTime.UtcNow > deadline)
        {
            Finish(false, "等待 ActiveRun 超时", 0, -1);
            return;
        }
        DungeonManager manager = UnityEngine.Object.FindAnyObjectByType<DungeonManager>();
        if (manager == null) return;
        if (manager.ActiveRun == null) return;
        if (manager.CurrentRoom == null) return;
        try
        {
            SaveService.ActiveRunData run = manager.ActiveRun;
            PlayerStats stats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
            Health health = UnityEngine.Object.FindAnyObjectByType<Health>();
            if (stats == null || health == null)
                throw new InvalidOperationException("找不到玩家生命或属性组件");
            if (!Mathf.Approximately(health.CurrentHealth, health.MaxHealth)
                || !Mathf.Approximately(stats.CurrentArmor, stats.MaxArmor)
                || stats.MaxArmor <= 0f || !run.resourcesInitialized)
                throw new InvalidOperationException(
                    $"狼人初始资源异常：HP={health.CurrentHealth}/{health.MaxHealth} " +
                    $"Armor={stats.CurrentArmor}/{stats.MaxArmor} initialized={run.resourcesInitialized}");
            if (!run.dungeonGraph.Validate(out string error))
                throw new InvalidOperationException("图无效：" + error);
            if (run.currentNodeId != run.dungeonGraph.StartNodeId)
                throw new InvalidOperationException("未从 Start 进入");
            int visited = 0;
            while (true)
            {
                DungeonGraphNode node = DungeonRouteRules.Current(run);
                if (node == null || manager.Rooms.Count != 1 || manager.CurrentRoom.Id != node.NodeId)
                    throw new InvalidOperationException("当前节点与单房实例不一致");
                visited++;
                if (node.Type == NodeType.Boss) break;
                if (!node.ObjectiveCompleted
                    && !DungeonRouteRules.TryCompleteObjective(run, node.NodeId))
                    throw new InvalidOperationException("目标完成失败：" + node.NodeId);
                if (!node.RewardResolved
                    && !DungeonRouteRules.TryResolveReward(run, node.NodeId))
                    throw new InvalidOperationException("领奖失败：" + node.NodeId);
                if (!manager.TryChooseNext(node.NextNodeIds[0]))
                    throw new InvalidOperationException("路线推进失败：" + node.NodeId);
            }
            manager.SaveActiveRun();
            SaveService.ActiveRunData restored = SaveService.LoadRun();
            if (restored == null || restored.currentNodeId != run.currentNodeId
                || !restored.dungeonGraph.Validate(out error))
                throw new InvalidOperationException("Boss 节点存档回读失败：" + error);
            if (visited < 10 || visited > 12)
                throw new InvalidOperationException("路径长度越界：" + visited);
            Finish(true, "狼人满生命/满护甲迁移与 Start 到 Boss 路线通过", visited, run.currentNodeId);
        }
        catch (Exception e)
        {
            Finish(false, e.ToString(), 0, -1);
        }
    }

    private static void Finish(bool passed, string message, int visited, int nodeId)
    {
        active = false;
        EditorApplication.update -= Tick;
        string reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath,
            "Temp", "V211SmokeResult.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(new Report
        {
            passed = passed, message = message, visited = visited, nodeId = nodeId,
        }, true));
        Debug.Log((passed ? "[V211Smoke] PASS " : "[V211Smoke] FAIL ") + message);
        RunManager runManager = UnityEngine.Object.FindAnyObjectByType<RunManager>();
        if (runManager != null) runManager.StopAllCoroutines();
        SaveService.EditorStorageRootOverride = null;
        if (!string.IsNullOrEmpty(tempRoot))
        {
            string path = Path.GetFullPath(tempRoot);
            if (path.StartsWith(Path.GetFullPath(Path.GetTempPath()),
                    StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
                Directory.Delete(path, true);
        }
        EditorApplication.isPlaying = false;
    }
}
#endif
