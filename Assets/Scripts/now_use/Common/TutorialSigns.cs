using UnityEngine;

/// <summary>
/// 出生房教程牌（M5·v1.0.0）：每局在出生房放置 3 块操作提示牌（纯代码 TextMesh，
/// 程序员美术零资产）。Roguelite 老玩家可无视，新玩家零学习成本。
/// DungeonManager.Generate 末尾调用。
/// </summary>
public static class TutorialSigns
{
    public static void Spawn(Room startRoom)
    {
        if (startRoom == null) return;
        Vector3 c = startRoom.Center;

        MakeSign(new Vector3(c.x - 3f, c.y + 2.5f, 0f),
            "WASD 移动\n左键 攻击\n鼠标 瞄准");
        MakeSign(new Vector3(c.x, c.y + 2.5f, 0f),
            "Space 冲刺（无敌帧）\nT 变身兽化\nU 魂商店");
        MakeSign(new Vector3(c.x + 3f, c.y + 2.5f, 0f),
            "杀怪掉金币\n商店房购物\n第 9 层 通关！");
    }

    private static void MakeSign(Vector3 pos, string text)
    {
        var go = new GameObject("TutorialSign");
        go.transform.position = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 22;
        tm.characterSize = 0.12f;   // v1.0.0 修复：TextMesh 世界字高 = fontSize/10 × characterSize——默认 1 时每字 2.2 单位高（巨大）。0.12 → 约 0.26 单位/字
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.9f, 0.6f, 0.9f);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 8;
    }
}
