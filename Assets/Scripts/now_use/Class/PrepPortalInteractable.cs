using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 准备场景进入地牢的传送门（v0.6.2 阶段 C，计划书 R4）。
/// E 交互：校验 RunStateCarrier.HasLoadout（职业+武器都已选定）→ SceneManager 加载地牢场景；
/// 未齐 → 提示"请先选择职业和武器"并拒绝。可重复交互（覆盖 Interact 不走一次性消耗）。
/// 视觉运行时代码构建：灰石门框 + 蓝色旋涡方块（DOTween 缓转，SetLink）。
/// </summary>
public class PrepPortalInteractable : Interactable
{
    private static readonly Color stoneGray = new Color(0.5f, 0.55f, 0.55f);
    private static readonly Color vortexBlue = new Color(0.2f, 0.5f, 0.95f);

    private static Sprite whiteSprite;

    [SerializeField] private string dungeonSceneName = "v0_7_ClassWeapon";

    /// <summary>运行时构建传送门（PrepRoomManager 调用）。</summary>
    public static PrepPortalInteractable Create(Vector3 position, Transform parent, string dungeonScene)
    {
        GameObject go = new GameObject("PrepPortal");
        go.transform.position = position;
        go.transform.SetParent(parent, true);

        // 先显式加碰撞体：基类 RequireComponent(typeof(Collider2D)) 是抽象类型，
        // 后加会让 Unity 在 AddComponent 时尝试自动补抽象组件抛 NullReferenceException。
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();

        PrepPortalInteractable portal = go.AddComponent<PrepPortalInteractable>();
        if (!string.IsNullOrEmpty(dungeonScene))
            portal.dungeonSceneName = dungeonScene;

        col.isTrigger = true;
        col.radius = 0.8f;

        portal.BuildVisual();
        return portal;
    }

    /// <summary>覆盖基类：可重复交互，不消耗。</summary>
    public override void Interact(Collider2D player)
    {
        RunStateCarrier carrier = RunStateCarrier.Ensure();
        if (!carrier.HasLoadout)
        {
            if (player != null && player.TryGetComponent(out PlayerInteractor interactor))
                interactor.ShowTemporaryHint("请先选择职业和武器");
            Debug.Log("[Run] 传送门拒绝：尚未选定职业和武器");
            return;
        }

        // 加载新场景前确保选择 UI 等静态状态不残留
        ClassSelectUI.Close();
        Debug.Log($"[Run] 进入地牢：{carrier.LastChosenClass.DisplayName} + {carrier.LastWeapon.DisplayName} → {dungeonSceneName}");
        SceneManager.LoadScene(dungeonSceneName);
    }

    /// <summary>一次性效果：本类不使用基类消耗流程（覆盖 Interact 后不会走到）。</summary>
    protected override void ApplyEffect(Collider2D player) { }

    private void BuildVisual()
    {
        // 石门框（左右立柱 + 门楣）
        CreateBlock("PillarL", new Vector2(0.3f, 1.6f), new Vector3(-0.65f, 0.8f, 0f), stoneGray);
        CreateBlock("PillarR", new Vector2(0.3f, 1.6f), new Vector3(0.65f, 0.8f, 0f), stoneGray);
        CreateBlock("Lintel", new Vector2(1.6f, 0.3f), new Vector3(0f, 1.55f, 0f), stoneGray);

        // 蓝色旋涡（缓转方块）
        SpriteRenderer vortex = CreateBlock("Vortex", new Vector2(0.9f, 0.9f), new Vector3(0f, 0.8f, 0f), vortexBlue);
        vortex.transform
            .DORotate(new Vector3(0f, 0f, -360f), 2.5f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
            .SetLink(vortex.gameObject);
    }

    private SpriteRenderer CreateBlock(string name, Vector2 size, Vector3 localPos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        return sr;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
        }
        return whiteSprite;
    }
}
