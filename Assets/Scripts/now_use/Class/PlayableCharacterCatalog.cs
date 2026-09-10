using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业角色定义目录。当前 MVP 只公开狼人；后续角色只能在此追加定义，
/// 禁止再建立独立 ClassId 选择链。
/// </summary>
public static class PlayableCharacterCatalog
{
    private static readonly string[] ResourcePaths =
    {
        "Characters/Character_Werewolf"
    };

    private static PlayableCharacterDefinition[] definitions;

    public static IReadOnlyList<PlayableCharacterDefinition> All
    {
        get
        {
            EnsureLoaded();
            return definitions;
        }
    }

    public static PlayableCharacterDefinition Get(PlayableCharacterId id)
    {
        EnsureLoaded();
        for (int i = 0; i < definitions.Length; i++)
        {
            PlayableCharacterDefinition definition = definitions[i];
            if (definition != null && definition.Id == id)
                return definition;
        }

        Debug.LogError($"[PlayableCharacter] 未找到职业角色定义：{id}");
        return null;
    }

    private static void EnsureLoaded()
    {
        if (definitions != null) return;

        definitions = new PlayableCharacterDefinition[ResourcePaths.Length];
        for (int i = 0; i < ResourcePaths.Length; i++)
        {
            definitions[i] = Resources.Load<PlayableCharacterDefinition>(ResourcePaths[i]);
            if (definitions[i] == null)
                Debug.LogError($"[PlayableCharacter] Resources/{ResourcePaths[i]} 加载失败。");
        }
    }
}
