using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.7 返工版 奖励挂点：全链多房间模式下，战斗/精英房清房 → 弹三选一
///（V2 §8.4 普通节点末尾一次主要选择；每房一次，订阅与 Generate/Cleanup 生命周期配对）。
/// 取代已撤销的 DungeonNodeRunner 挂点（单 Active 房间架构已按用户要求回退）。
/// </summary>
public static class RoomRewardHook
{
    private static readonly List<KeyValuePair<Room, Action<Room>>> subscriptions =
        new List<KeyValuePair<Room, Action<Room>>>();

    /// <summary>DungeonManager.Generate 末尾调用：订阅本层全部战斗/精英房。</summary>
    public static void Attach(IReadOnlyDictionary<int, Room> rooms)
    {
        Detach();
        if (rooms == null) return;
        foreach (KeyValuePair<int, Room> kv in rooms)
        {
            Room room = kv.Value;
            if (room == null) continue;
            if (room.Type != RoomType.Combat && room.Type != RoomType.Elite) continue;

            Room captured = room;
            Action<Room> handler = _ => OnRoomCleared(captured);
            room.OnRoomCleared += handler;
            subscriptions.Add(new KeyValuePair<Room, Action<Room>>(room, handler));
        }
    }

    /// <summary>Cleanup/重开调用：退订全部（配对红线）。</summary>
    public static void Detach()
    {
        foreach (KeyValuePair<Room, Action<Room>> kv in subscriptions)
            if (kv.Key != null) kv.Key.OnRoomCleared -= kv.Value;
        subscriptions.Clear();
    }

    private static void OnRoomCleared(Room room)
    {
        PlayerStats stats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        Health health = stats != null ? stats.GetComponent<Health>() : null;
        bool elite = room.Type == RoomType.Elite;

        List<NodeRewardService.RewardOption> rewards =
            NodeRewardService.Roll(room.Id * 977 + (elite ? 50 : 0), elite);
        RewardChoiceUI.Show(rewards, opt => Apply(opt, stats, health));
    }

    private static void Apply(NodeRewardService.RewardOption opt, PlayerStats stats, Health health)
    {
        switch (opt.Kind)
        {
            case NodeRewardService.RewardKind.Coins:
                stats?.AddCoins(Mathf.RoundToInt(opt.Value));
                break;
            case NodeRewardService.RewardKind.Heal:
                health?.Heal(opt.Value);
                break;
            case NodeRewardService.RewardKind.AttackUp:
                if (stats != null) stats.PermDamageMult += opt.Value;   // 本局累计，死亡随 Stats 重建重置
                break;
        }
        Debug.Log($"[RoomReward] 应用奖励：{opt.Title}（{opt.Description}）");
    }
}
