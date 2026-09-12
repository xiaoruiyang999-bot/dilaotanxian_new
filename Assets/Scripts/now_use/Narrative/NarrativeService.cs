using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.7 叙事碎片系统（V2 §2.3/§2.4）：必得碎片保底（每 Run 至少一条关键进展）+
/// 已读状态持久（PlayerPrefs 位集，局外档案雏形）。纯静态数据层，投放 UI 由
/// NarrativePanelUI 消费；文本按 V2 §2 世界观（无名记忆/VII 徽章/引导声音）。
/// </summary>
public static class NarrativeService
{
    public class Fragment
    {
        public string Id;
        public string Title;
        public string[] Lines;      // 正文分段
        public bool IsOpening;      // 开场序列（首次进守灯厅连播）
    }

    private const string KeyRead = "narrative_read";

    public static readonly List<Fragment> Library = new List<Fragment>
    {
        // 开场（V2 §2.1）
        new Fragment
        {
            Id = "open_awaken", IsOpening = true, Title = "无名之地 · 苏醒",
            Lines = new[]
            {
                "你在冰冷的石地上醒来。这里没有名字——至少你想不起来。",
                "身上只留着一枚徽章，金属边缘刻着「VII」。",
                "是实验的序号？骑士的编号？还是封印的层级？你无从得知。",
            },
        },
        new Fragment
        {
            Id = "open_voice", IsOpening = true, Title = "无名之地 · 声音",
            Lines = new[]
            {
                "「去最深处。」",
                "一个女人的声音在你耳边响起，平静得像在陈述天气。",
                "她不回答你的问题。但每次你倒下，这声音都在——比你自己的名字更可靠。",
            },
        },
        // 进展碎片（每 Run 必得一条，按队列推进——多局拼合真相）
        new Fragment
        {
            Id = "prog_lamp", Title = "守灯人的房间",
            Lines = new[]
            {
                "守灯厅的灯还亮着。灯下刻着一行小字：",
                "「灯在，路就在。灯灭之人不再记得回来的方向。」",
                "你数了数墙上的凹痕——有人在这里等过很多个「回来」。",
            },
        },
        new Fragment
        {
            Id = "prog_border", Title = "荒废边境",
            Lines = new[]
            {
                "边境的哨塔里空无一人，桌上摆着没下完的棋。",
                "白棋一方只剩一枚兵，被推到了底线之前最后一格。",
                "棋盘旁的批注写着：「它升变的时候，谁也拦不住。」",
            },
        },
        new Fragment
        {
            Id = "prog_gatekeeper", Title = "守门人的叹息",
            Lines = new[]
            {
                "格兰在门前站了很久。他不拦你，只是在你看向深处时摇头。",
                "石门内侧刻满了划痕，每一道都是一次「放行」。",
                "最新的一道还很浅——像是在等谁来补完。",
            },
        },
        new Fragment
        {
            Id = "prog_seventh", Title = "第 VII 号柜",
            Lines = new[]
            {
                "监牢的柜子编到了 VII。前六个柜门都开着，里面只剩灰。",
                "第七个柜门上了锁，锁孔的形状——和你的徽章一样。",
                "你没有打开它。还不是时候。",
            },
        },
        new Fragment
        {
            Id = "prog_voice_name", Title = "声音的名字",
            Lines = new[]
            {
                "那一层的风很大，你几乎听不清她。",
                "「……如果你走到最深处，替我问一句话。」",
                "她没说是哪一句。你隐约觉得，见到它时你会知道。",
            },
        },
    };

    /// <summary>已读片段 Id 集（PlayerPrefs 位集持久）。</summary>
    public static HashSet<string> ReadIds()
    {
        var set = new HashSet<string>();
        foreach (string part in (PlayerPrefs.GetString(KeyRead, "")).Split(','))
            if (!string.IsNullOrEmpty(part)) set.Add(part);
        return set;
    }

    private static void SaveRead(HashSet<string> set)
    {
        PlayerPrefs.SetString(KeyRead, string.Join(",", set));
        PlayerPrefs.Save();
    }

    /// <summary>标记已读（幂等）。</summary>
    public static void MarkRead(string id)
    {
        var set = ReadIds();
        if (set.Add(id)) SaveRead(set);
    }

    /// <summary>开场序列：未读的 IsOpening 片段（按库序）。</summary>
    public static List<Fragment> PendingOpening()
    {
        var read = ReadIds();
        var list = new List<Fragment>();
        foreach (Fragment f in Library)
            if (f.IsOpening && !read.Contains(f.Id)) list.Add(f);
        return list;
    }

    /// <summary>本 Run 必得碎片：第一条未读的非开场片段（保底推进，V2 §2.3）。</summary>
    public static Fragment NextRequiredProgress()
    {
        var read = ReadIds();
        foreach (Fragment f in Library)
            if (!f.IsOpening && !read.Contains(f.Id)) return f;
        return null;   // 全部读完：多局重释阶段，不再强制投放
    }

    /// <summary>测试/调试：清空已读。</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyRead);
        PlayerPrefs.Save();
    }
}
