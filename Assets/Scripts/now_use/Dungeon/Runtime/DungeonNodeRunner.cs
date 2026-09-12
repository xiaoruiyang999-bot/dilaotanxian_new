using System.Collections;
using UnityEngine;

/// <summary>
/// v2.0.5 第二批 节点推进器（V2 §11.1/§3.1 不可逆）：LinearHorizontal 单房模式的运行状态机。
/// 完成判定：战斗/精英/Boss = Room.OnRoomCleared；非战斗（宝箱/商店/事件/恢复）= 玩家抵达
/// 房间右侧出口区。完成 → 标记 Completed → 弹 DAG 选择（可选=CurrentNodeId 的 Next）→
/// 选择 → DungeonManager.TransitionToNode 黑屏过渡重建。Boss 节点不弹选择——清房奖励与
/// 下一层传送门由既有结算链负责（RunManager.OnBossCleared + portalPrefab）。
/// </summary>
public class DungeonNodeRunner : MonoBehaviour
{
    private DungeonManager manager;
    private Room current;
    private bool awaitingChoice;      // 完成已弹选择，等玩家点
    private bool reachedExit;         // 非战斗房的右侧到达标记
    private Transform player;

    private void Awake() => manager = GetComponent<DungeonManager>();

    private void OnEnable() => StartCoroutine(InitWhenReady());

    private IEnumerator InitWhenReady()
    {
        // DungeonManager.Start 先跑 Generate（含首个节点房），延迟到它完成后再订阅
        yield return null;
        yield return null;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        SubscribeCurrent();
    }

    private void OnDestroy() => UnsubscribeCurrent();

    private void Update()
    {
        if (awaitingChoice || current == null) return;
        DungeonGraphNode node = manager.Graph?.Get(manager.CurrentNodeId);
        if (node == null) return;

        // 非战斗节点：抵达右侧出口区即完成（战斗类由清房事件驱动，此处不重复触发）
        bool combatLike = node.Type == NodeType.Combat || node.Type == NodeType.Elite
            || node.Type == NodeType.Boss || node.Type == NodeType.Start;
        if (combatLike || reachedExit) return;

        if (player != null && player.position.x >= current.Bounds.xMax - 2.5f)
        {
            reachedExit = true;
            CompleteNode(node);
        }
    }

    private void SubscribeCurrent()
    {
        UnsubscribeCurrent();
        if (manager?.Graph == null) return;
        DungeonGraphNode node = manager.Graph.Get(manager.CurrentNodeId);
        if (node == null) return;
        reachedExit = false;

        if (manager.Rooms.TryGetValue(0, out Room room) && room != null)
        {
            current = room;
            current.OnRoomCleared += OnCurrentCleared;
        }
    }

    private void UnsubscribeCurrent()
    {
        if (current != null) current.OnRoomCleared -= OnCurrentCleared;
        current = null;
    }

    private void OnCurrentCleared(Room room)
    {
        DungeonGraphNode node = manager?.Graph?.Get(manager.CurrentNodeId);
        if (node == null) return;
        if (node.Type == NodeType.Boss) return;   // Boss：奖励/传送门链接管，不弹选择
        CompleteNode(node);
    }

    private void CompleteNode(DungeonGraphNode node)
    {
        if (awaitingChoice) return;
        node.Completed = true;
        awaitingChoice = true;

        // v2.0.6 战斗/精英先弹奖励三选一（V2 §3 核心循环：战斗→奖励→选择），关闭后再选路线
        if (node.Type == NodeType.Combat || node.Type == NodeType.Elite)
        {
            var rewards = NodeRewardService.Roll(node.NodeId * 977 + manager.CurrentNodeId,
                elite: node.Type == NodeType.Elite);
            RewardChoiceUI.Show(rewards, ApplyReward);
            StartCoroutine(OpenChoiceWhenRewardClosed());
            return;
        }

        if (node.NextNodeIds.Count == 0) return;
        StartCoroutine(OpenChoiceDelayed());
    }

    /// <summary>奖励应用（UI 薄层：数值写在此处，按类型分派）。</summary>
    private void ApplyReward(NodeRewardService.RewardOption opt)
    {
        PlayerStats stats = player != null ? player.GetComponent<PlayerStats>() : null;
        switch (opt.Kind)
        {
            case NodeRewardService.RewardKind.Coins:
                stats?.AddCoins(Mathf.RoundToInt(opt.Value));
                break;
            case NodeRewardService.RewardKind.Heal:
                if (player != null) player.GetComponent<Health>()?.Heal(opt.Value);
                break;
            case NodeRewardService.RewardKind.AttackUp:
                if (stats != null) stats.PermDamageMult += opt.Value;   // 本局累计（死亡/新 Run 随 Stats 重建重置）
                break;
        }
        Debug.Log($"[NodeReward] 应用奖励：{opt.Title}（{opt.Description}）");
    }

    /// <summary>等奖励界面关闭后弹路线选择（无后继的末节点跳过）。</summary>
    private System.Collections.IEnumerator OpenChoiceWhenRewardClosed()
    {
        while (RewardChoiceUI.IsOpen) yield return null;
        yield return new WaitForSeconds(0.3f);
        DungeonGraphNode node = manager?.Graph?.Get(manager.CurrentNodeId);
        if (node != null && node.NextNodeIds.Count > 0)
            DungeonMapUI.OpenInteractive(manager.Graph, manager.CurrentNodeId, OnPickNode);
        else awaitingChoice = false;
    }

    private IEnumerator OpenChoiceDelayed()
    {
        yield return new WaitForSeconds(0.6f);   // 清房反馈呼吸
        DungeonMapUI.OpenInteractive(manager.Graph, manager.CurrentNodeId, OnPickNode);
    }

    private void OnPickNode(int nextId)
    {
        awaitingChoice = false;
        UnsubscribeCurrent();
        manager.TransitionToNode(nextId, SubscribeCurrent);
    }
}
