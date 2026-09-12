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

        var rewards =
            NodeRewardService.Roll(room.Id * 977 + (elite ? 50 : 0), elite);
        PlayerWeaponHolder holder = stats != null ? stats.GetComponent<PlayerWeaponHolder>() : null;
        // 填充武器变体项（从角色武器池轮换，剔除当前持有）
        for (int i = 0; i < rewards.Count; i++)
        {
            var opt = rewards[i];
            if (opt.Kind == NodeRewardService.RewardKind.WeaponVariant)
            {
                NodeRewardService.FillWeaponVariant(ref opt,
                    stats != null && stats.CurrentPlayableCharacter != null
                        ? stats.CurrentPlayableCharacter.AvailableWeapons : null,
                    holder != null && holder.Current != null ? holder.Current.Data : null,
                    room.Id * 31 + 7);
                if (opt.Weapon == null) { rewards.RemoveAt(i); i--; continue; }   // 无可换变体则移除该项
            }
            if (opt.Kind == NodeRewardService.RewardKind.Relic)
            {
                NodeRewardService.FillRelic(ref opt, RelicRuntime.Run, room.Id * 53 + 3);
                if (opt.Relic == null) { rewards.RemoveAt(i); i--; continue; }   // 池空/全持有则移除
            }
            rewards[i] = opt;
        }
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
            case NodeRewardService.RewardKind.WeaponVariant:
                PlayerWeaponHolder weaponHolder = stats != null ? stats.GetComponent<PlayerWeaponHolder>() : null;
                weaponHolder?.Equip(opt.Weapon);   // 旧武器自动入背包（v0.7.2 挤出掉落规则）
                break;
            case NodeRewardService.RewardKind.Relic:
                RelicRuntime.Run.TryAdd(opt.Relic);   // 本局持有；数值经聚合通道自动生效
                break;
        }
        Debug.Log($"[RoomReward] 应用奖励：{opt.Title}（{opt.Description}）");
    }
}
