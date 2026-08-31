using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 全局 TMP 字体提供（v0.6.2 阶段 B TMP 迁移）。
/// 关键：必须从字体<b>文件</b>加载（new Font(path)，含字体数据），TMP 才能栅格化字形；
/// CreateDynamicFontFromOSFont 按名字创建的 Font 不含字体数据，TMP 会加载失败并回退
/// LiberationSans SDF（无中文 → 满屏"字符未找到"警告、汉字变方块）。
/// 全局缓存一份；全部失败时 fallback 到 TMP 内置字体（无中文，仅兜底）。
/// </summary>
public static class TMPFontProvider
{
    private static TMP_FontAsset fontAsset;

    // 按优先级尝试的中文字体文件（微软雅黑 → 黑体 → 宋体）
    private static readonly string[] fontFilePaths =
    {
        "C:/Windows/Fonts/msyh.ttc",
        "C:/Windows/Fonts/simhei.ttf",
        "C:/Windows/Fonts/simsun.ttc"
    };

    public static TMP_FontAsset Font
    {
        get
        {
            if (fontAsset == null)
            {
                UnityEngine.Font fileFont = LoadFontFromFile();
                if (fileFont != null)
                {
                    fontAsset = TMP_FontAsset.CreateFontAsset(
                        fileFont, 32, 4, GlyphRenderMode.SDFAA,
                        1024, 1024, AtlasPopulationMode.Dynamic, true);
                }
                if (fontAsset == null)
                {
                    fontAsset = TMP_Settings.defaultFontAsset;
                    Debug.LogWarning("[TMP] 中文字体加载失败，已回退内置字体（中文将无法显示）。");
                    if (fontAsset == null)
                        Debug.LogError("[TMP] 字体创建失败且无内置 fallback，TMP 文字将无法显示。");
                }
            }
            return fontAsset;
        }
    }

    /// <summary>从字体文件加载（含字体数据）。TTC 合集取集合内第一个字体。</summary>
    private static UnityEngine.Font LoadFontFromFile()
    {
        foreach (string path in fontFilePaths)
        {
            if (!System.IO.File.Exists(path)) continue;
            UnityEngine.Font font = new UnityEngine.Font(path);
            if (font != null)
            {
                Debug.Log($"[TMP] 中文字体已加载：{path}");
                return font;
            }
        }
        return null;
    }
}
