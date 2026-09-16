using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// v2.0.11 训练房管理器（任务计划 §2/§3）：场景入口——程序化建地/建墙/锁相机、
/// 自动初始化狼人玩家（跳过准备房选择）、快捷键刷怪与功能控制、Boss 石柱布置。
/// 纯开发工具：不进 Build Settings、不修改正式玩法代码。
/// 键位走 Keyboard.current 设备直读（训练房按键不属于正式键位表）。
/// </summary>
public class TrainingRoomManager : MonoBehaviour
{
    [Header("房间尺寸")]
    [SerializeField] private Vector2 roomSize = new Vector2(40f, 24f);
    [SerializeField] private Vector3 playerSpawn = new Vector3(-14f, 0f, 0f);

    private static readonly Color floorColor = new Color(0.14f, 0.20f, 0.26f);
    private static readonly Color wallColor = new Color(0.10f, 0.14f, 0.17f);

    private Transform spawnRoot;
    private PlayerStats playerStats;
    private Health playerHealth;
    private WerewolfRage playerRage;
    private BossArenaState arenaState;
    private TrainingStatsPanel statsPanel;
    private bool invincible;

    // 刷怪 Prefab 名 → 快捷键映射（§3.1）
    private static readonly (string prefab, int key, string label)[] SpawnTable =
    {
        ("Enemy", 1, "近战阻挡者"),
        ("Enemy_Ranged", 2, "后排远程"),
        ("Enemy_Charger", 3, "冲锋"),
        ("Enemy_Skirmisher", 4, "游击"),
        ("Enemy_Summoner", 5, "召唤"),
        ("Enemy_Elite", 6, "精英"),
        ("Enemy_Minion", 7, "群聚小怪"),
    };

    void Start()
    {
        BuildRoom();
        InitPlayer();
        SetupCamera();
        SetupPanels();
        Debug.Log("[Training] 训练房就绪：1~7 刷怪 / B 刷 Boss / K 清场 / I 无敌 / H 回满 / F6 面板");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 刷怪键
        foreach (var (prefabName, key, label) in SpawnTable)
        {
            var keyCtrl = DigitKey(kb, key);
            if (keyCtrl != null && keyCtrl.wasPressedThisFrame)
                SpawnEnemy(prefabName, key == 7 ? 4 : 1, label);
        }

        if (kb.bKey.wasPressedThisFrame) SpawnBoss();
        if (kb.kKey.wasPressedThisFrame) KillAll();
        if (kb.iKey.wasPressedThisFrame) ToggleInvincible();
        if (kb.hKey.wasPressedThisFrame) FullRestore();
        if (kb.f6Key.wasPressedThisFrame) statsPanel?.Toggle();
    }

    // ---------- 场景构建（§2.1） ----------

    private void BuildRoom()
    {
        CreateBlock("Floor", roomSize, Vector3.zero, floorColor, -1);
        float w = roomSize.x, h = roomSize.y;
        const float t = 0.5f;
        CreateBlock("WallN", new Vector2(w + t * 2f, t), new Vector3(0f, h * 0.5f + t * 0.5f), wallColor, solid: true);
        CreateBlock("WallS", new Vector2(w + t * 2f, t), new Vector3(0f, -h * 0.5f - t * 0.5f), wallColor, solid: true);
        CreateBlock("WallW", new Vector2(t, h), new Vector3(-w * 0.5f - t * 0.5f, 0f), wallColor, solid: true);
        CreateBlock("WallE", new Vector2(t, h), new Vector3(w * 0.5f + t * 0.5f, 0f), wallColor, solid: true);

        spawnRoot = new GameObject("TrainingSpawnRoot").transform;
        spawnRoot.SetParent(transform, false);
    }

    private void SetupCamera()
    {
        CameraFollow.SetMapBounds(new Rect(
            -roomSize.x * 0.5f - 0.5f, -roomSize.y * 0.5f - 0.5f,
            roomSize.x + 1f, roomSize.y + 1f));
        if (Camera.main != null && Camera.main.TryGetComponent(out CameraFollow cam))
            cam.SnapToTarget();
    }

    private void SetupPanels()
    {
        statsPanel = gameObject.AddComponent<TrainingStatsPanel>();
        statsPanel.Initialize(this);
    }

    // ---------- 玩家初始化（§2.2） ----------

    private void InitPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // 从 Prefab 实例化（训练房不依赖准备房——直接加载 Player Prefab）
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
            if (prefab == null)
            {
                Debug.LogError("[Training] 找不到 Player（场景无 + Prefabs/Player 缺失）");
                return;
            }
            player = Instantiate(prefab, playerSpawn, Quaternion.identity);
        }
        else
        {
            player.transform.position = playerSpawn;
        }

        playerStats = player.GetComponent<PlayerStats>();
        playerHealth = player.GetComponent<Health>();
        playerRage = player.GetComponent<WerewolfRage>();

        // 跳过选择页：直接写入狼人并应用全套（§2.2 五步）
        var carrier = RunStateCarrier.Ensure();
        carrier.SetPlayableCharacter(PlayableCharacterId.Werewolf);

        var definition = carrier.ChosenPlayableCharacter;
        if (definition != null)
        {
            playerStats.ApplyPlayableCharacter(definition);
            CharacterSelectUI.ApplyCharacterRuntime(player, PlayableCharacterId.Werewolf);
        }

        // 装备基础刺刀
        var holder = player.GetComponent<PlayerWeaponHolder>();
        if (holder == null) holder = player.AddComponent<PlayerWeaponHolder>();
        WeaponData weapon = LoadWeapon("Weapon_Werewolf_Bayonet");
        if (weapon != null) holder.Equip(weapon);
    }

    private static WeaponData LoadWeapon(string name)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/Data/Weapon/{name}.asset");
#else
        return Resources.Load<WeaponData>($"Data/Weapon/{name}");
#endif
    }

    // ---------- 刷怪（§3.1/§3.2） ----------

    private void SpawnEnemy(string prefabName, int count, string label)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = LoadEnemyPrefab(prefabName);
            if (prefab == null) continue;

            Vector3 pos = FindSpawnPos();
            var go = Instantiate(prefab, pos, Quaternion.identity, spawnRoot);
            go.name = $"Training_{prefabName}_{Time.frameCount}_{i}";
            statsPanel?.Track(go.GetComponent<EnemyHealth>(), label);
        }
        Debug.Log($"[Training] 刷出 {label} ×{count}");
    }

    private void SpawnBoss()
    {
        GameObject prefab = LoadEnemyPrefab("Enemy_Boss");
        if (prefab == null) { Debug.LogWarning("[Training] Enemy_Boss Prefab 缺失"); return; }

        // Boss 刷在房间右端中央（§3.2）
        Vector3 pos = new Vector3(roomSize.x * 0.3f, 0f, 0f);
        var go = Instantiate(prefab, pos, Quaternion.identity, spawnRoot);
        go.name = "Training_GrandBoss";

        // 全套组件自举（§5.1）
        GrandBossBrain.EnsureOn(go);

        // 石柱布置（§5.2）：四角内缩 4 格
        SetupPillars();
        statsPanel?.Track(go.GetComponent<EnemyHealth>(), "格兰 Boss");

        Debug.Log("[Training] 格兰 Boss 刷出（全套组件 + 4 石柱）");
    }

    private void SetupPillars()
    {
        if (arenaState != null) { arenaState.ResetAll(); return; }

        var ago = new GameObject("TrainingArenaState");
        ago.transform.SetParent(transform, false);
        arenaState = ago.AddComponent<BossArenaState>();

        float ix = roomSize.x * 0.5f - 4f;
        float iy = roomSize.y * 0.5f - 4f;
        Vector2[] corners = { new(-ix, -iy), new(ix, -iy), new(ix, iy), new(-ix, iy) };
        for (int i = 0; i < corners.Length; i++)
        {
            var pgo = new GameObject($"TrainingPillar_{i}");
            pgo.transform.SetParent(ago.transform, false);
            pgo.transform.position = new Vector3(corners[i].x, corners[i].y, 0f);
            var sr = pgo.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = new Color(0.28f, 0.34f, 0.33f);
            sr.sortingOrder = 4;
            pgo.transform.localScale = new Vector3(1.8f, 2.6f, 1f);
            var col = pgo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 2.6f);
            arenaState.RegisterPillar(i, corners[i], pgo, col);
        }
    }

    // ---------- 功能键（§3.3） ----------

    private void KillAll()
    {
        int killed = 0;
        foreach (EnemyHealth e in spawnRoot.GetComponentsInChildren<EnemyHealth>())
        {
            if (e != null && !e.IsDead) { e.TakeDamage(float.MaxValue); killed++; }
        }
        arenaState?.ResetAll();
        Debug.Log($"[Training] 清场：击杀 {killed} 只");
    }

    private void ToggleInvincible()
    {
        invincible = !invincible;
        if (playerHealth != null) playerHealth.SetInvincible(invincible);
        Debug.Log($"[Training] 无敌：{(invincible ? "开" : "关")}");
    }

    private void FullRestore()
    {
        if (playerHealth != null) playerHealth.Initialize(playerStats.MaxHP);
        if (playerStats != null) playerStats.ModifyArmor(playerStats.MaxArmor);
        if (playerRage != null) playerRage.ResetRage();
        if (playerStats != null) playerStats.AddMana(playerStats.MaxMana);
        Debug.Log("[Training] 玩家状态回满");
    }

    // ---------- 工具 ----------

    private GameObject LoadEnemyPrefab(string name)
    {
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/{name}");
        if (prefab == null && Application.isEditor)
        {
            // 编辑器路径兜底（Prefab 在 Assets/Prefabs/ 不在 Resources）
            Debug.LogWarning($"[Training] Resources/Prefabs/{name} 缺失——训练房要求 Prefab 在 Resources/Prefabs/");
        }
        return prefab;
    }

    private Vector3 FindSpawnPos()
    {
        // 房间右半区随机（§3.1）——训练房无 Room 对象，手动合法区采样
        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(2f, roomSize.x * 0.5f - 2f);
            float y = Random.Range(-roomSize.y * 0.5f + 2f, roomSize.y * 0.5f - 2f);
            var pos = new Vector3(x, y, 0f);
            var filter = new ContactFilter2D
            {
                layerMask = LayerMask.GetMask("Default", "Enemy", "Obstacle"),
                useLayerMask = true, useTriggers = false,
            };
            var buf = new Collider2D[4];
            if (Physics2D.OverlapCircle(pos, 0.5f, filter, buf) == 0)
                return pos;
        }
        return new Vector3(5f, 0f, 0f);   // 兜底
    }

    internal static KeyControl DigitKey(Keyboard kb, int digit)
    {
        return digit switch
        {
            1 => kb.digit1Key, 2 => kb.digit2Key, 3 => kb.digit3Key, 4 => kb.digit4Key,
            5 => kb.digit5Key, 6 => kb.digit6Key, 7 => kb.digit7Key,
            _ => null,
        };
    }

    private void CreateBlock(string name, Vector2 size, Vector3 localPos, Color color, int sortingOrder = 0, bool solid = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        if (solid)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }
    }

    private static Sprite whiteSprite;
    internal static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            whiteSprite.name = "RT_TrainingWhite";
        }
        return whiteSprite;
    }

    // 面板访问器
    internal PlayerStats Stats => playerStats;
    internal Health PlayerHealth => playerHealth;
    internal WerewolfRage Rage => playerRage;
    internal Transform SpawnRoot => spawnRoot;
}
