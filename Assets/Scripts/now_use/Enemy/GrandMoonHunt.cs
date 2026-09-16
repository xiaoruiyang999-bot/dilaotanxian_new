using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.10 批2-3 残月围猎（开发文档 §六/招式二）：
/// 退距→选绕场方向（顺/逆时针）→沿房间外围四足奔跑→按距离留月痕（上限）→
/// 中途扑击锁定（方向固定不追踪）→落地后月痕依生成顺序逐个爆发→短暂恢复。
/// 月痕被站立石柱截断（柱后安全缺口）;碎石降低奔跑速度;对象池管理月痕。
/// </summary>
public class GrandMoonHunt : MonoBehaviour
{
    [Header("围猎参数（文档 §六 测试值）")]
    [SerializeField, Min(0f)] private float prepTime = 1.2f;         // 退距+选方向
    [SerializeField, Min(0f)] private float runSpeed = 8f;
    [SerializeField, Min(0f)] private float moonMarkSpacing = 2.5f;  // 月痕间距
    [SerializeField, Min(1)] private int maxMoonMarks = 14;
    [SerializeField, Min(0f)] private float moonMarkDamage = 22f;
    [SerializeField, Min(0f)] private float moonMarkRadius = 1.2f;
    [SerializeField, Min(0f)] private float markBurstInterval = 0.3f;
    [SerializeField, Min(0f)] private float moonMarkTelegraph = 0.5f;
    [SerializeField, Min(0f)] private float pounceDamage = 90f;
    [SerializeField, Min(0f)] private float pounceRadius = 1.6f;
    [SerializeField, Min(0f)] private float recoverTime = 1.8f;

    [Header("组件")]
    [SerializeField] private BossArenaState arena;
    [SerializeField] private BossTelegraphController telegraph;
    [SerializeField] private LayerMask playerMask;

    private Rigidbody2D rb;
    private bool cancelled;

    // 月痕对象池（文档 §六："使用对象池管理月痕，避免频繁实例化销毁"）
    private readonly List<MoonMark> markPool = new List<MoonMark>();

    private class MoonMark
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public Vector2 Position;
        public int Sequence;
        public bool Active;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (telegraph == null) telegraph = GetComponent<BossTelegraphController>();
    }

    public void Wire(BossArenaState arenaState, BossTelegraphController tele, LayerMask player)
    {
        arena = arenaState; telegraph = tele; playerMask = player;
    }

    public void Cancel()
    {
        cancelled = true;
        HideAllMarks();
    }

    /// <summary>围猎主流程。</summary>
    public IEnumerator Run(Transform player, GrandBossBrain brain)
    {
        cancelled = false;

        // ① 退距+选方向
        yield return new WaitForSeconds(prepTime);
        if (cancelled) yield break;

        // 绕场路径：房间 Bounds 内缩一圈的矩形
        Rect bounds = GetRoomBounds();
        bool clockwise = Random.value < 0.5f;
        var path = BuildPerimeterPath(bounds, clockwise);

        // ② 沿路径奔跑+留月痕
        float distSinceMark = 0f;
        int markCount = 0;
        Vector2 pounceTarget = Vector2.zero;
        bool pounceLocked = false;
        int pathIndex = 0;

        while (markCount < maxMoonMarks && !cancelled)
        {
            Vector2 target = path[pathIndex % path.Count];
            Vector2 pos = rb.position;
            Vector2 dir = (target - pos).normalized;

            float speed = runSpeed * (arena != null && arena.IsInRubble(pos)
                ? BossArenaState.RubbleSlowMultiplier : 1f);
            float step = speed * Time.deltaTime;
            rb.MovePosition(pos + dir * step);
            distSinceMark += step;

            // 月痕：按间距生成（不每帧）
            if (distSinceMark >= moonMarkSpacing)
            {
                distSinceMark = 0f;
                SpawnMoonMark(pos, markCount);
                markCount++;
            }

            // 到路径点切下一段
            if (Vector2.Distance(rb.position, target) < 1f)
                pathIndex++;

            // 中途扑击：绕了约半程后掷中
            if (!pounceLocked && markCount >= maxMoonMarks / 2 && Random.value < 0.02f)
            {
                pounceTarget = player.position;   // 锁定玩家位置（不追踪）
                pounceLocked = true;
                break;
            }

            yield return null;
        }

        if (cancelled) { HideAllMarks(); yield break; }

        // ③ 扑击：锁定方向→直线冲向玩家位置
        if (pounceLocked && player != null)
        {
            telegraph?.ShowLine(rb.position, (pounceTarget - rb.position).normalized,
                Vector2.Distance(rb.position, pounceTarget), 0.4f);
            Vector2 pDir = (pounceTarget - rb.position).normalized;
            float pDist = Vector2.Distance(rb.position, pounceTarget);
            float travelled = 0f;
            while (travelled < pDist && !cancelled)
            {
                float step = (chargePounceSpeed()) * Time.deltaTime;
                rb.MovePosition(rb.position + pDir * step);
                travelled += step;

                // 扑击命中
                Collider2D hit = Physics2D.OverlapCircle(rb.position, pounceRadius, playerMask);
                if (hit != null && hit.TryGetComponent(out IDamageable dmg))
                {
                    dmg.TakeDamage(pounceDamage);
                    break;
                }
                yield return null;
            }
            telegraph?.HideAll();
        }

        // ④ 月痕依序爆发
        yield return BurstMarksSequentially();
        if (cancelled) yield break;

        // ⑤ 短暂恢复
        yield return new WaitForSeconds(recoverTime);
    }

    /// <summary>月痕按生成顺序逐个爆发（每次间隔 markBurstInterval）。</summary>
    private IEnumerator BurstMarksSequentially()
    {
        var active = new List<MoonMark>();
        foreach (MoonMark m in markPool)
            if (m.Active) active.Add(m);
        active.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));

        foreach (MoonMark m in active)
        {
            if (cancelled) break;
            // 爆发预警→结算
            yield return new WaitForSeconds(markBurstInterval);
            Collider2D hit = Physics2D.OverlapCircle(m.Position, moonMarkRadius, playerMask);
            if (hit != null && hit.TryGetComponent(out IDamageable dmg))
                dmg.TakeDamage(moonMarkDamage);
            // 爆发视觉（占位：闪烁后隐）
            if (m.Sr != null) m.Sr.color = new Color(1f, 0.5f, 0.3f, 0.9f);
            yield return new WaitForSeconds(0.1f);
            DeactivateMark(m);
        }
    }

    /// <summary>月痕：站位是否被站立石柱截断（文档：Intact/Cracked 截断，Collapsed 不截断）。</summary>
    private bool IsBlockedByPillar(Vector2 pos)
    {
        if (arena == null) return false;
        var pillar = arena.FindNearest(pos, s => s != BossArenaState.PillarState.Collapsed);
        return pillar != null && Vector2.Distance(pos, pillar.Center) < 1.5f;
    }

    private void SpawnMoonMark(Vector2 pos, int sequence)
    {
        if (IsBlockedByPillar(pos)) return;   // 柱后安全缺口

        MoonMark mark = GetPooledMark();
        mark.Position = pos;
        mark.Sequence = sequence;
        mark.Active = true;
        mark.Go.transform.position = pos;
        mark.Go.SetActive(true);
        if (mark.Sr != null) mark.Sr.color = new Color(0.7f, 0.5f, 0.9f, 0.4f);   // 月痕紫
    }

    private MoonMark GetPooledMark()
    {
        foreach (MoonMark m in markPool)
            if (!m.Active) return m;
        var mark = new MoonMark
        {
            Go = new GameObject("MoonMark"),
            Sr = null,
        };
        mark.Go.transform.SetParent(transform, false);
        var sr = mark.Go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(0.7f, 0.5f, 0.9f, 0.4f);
        sr.sortingOrder = 2;
        mark.Go.transform.localScale = new Vector3(moonMarkRadius * 2f, moonMarkRadius * 2f, 1f);
        mark.Sr = sr;
        markPool.Add(mark);
        return mark;
    }

    private void DeactivateMark(MoonMark m)
    {
        m.Active = false;
        if (m.Go != null) m.Go.SetActive(false);
    }

    private void HideAllMarks()
    {
        foreach (MoonMark m in markPool) DeactivateMark(m);
    }

    private Rect GetRoomBounds()
    {
        // 从 Brain 或自身向上找 Room（批2 简化：用 Boss 当前位置的固定包围盒）
        var brain = GetComponent<GrandBossBrain>();
        if (brain == null) return new Rect(-10, -6, 20, 12);
        var room = FindAnyObjectByType<Room>();
        if (room == null) return new Rect(-10, -6, 20, 12);
        return new Rect(room.Bounds.xMin + 2f, room.Bounds.yMin + 2f,
            room.Bounds.width - 4f, room.Bounds.height - 4f);
    }

    /// <summary>房间外围路径（四角矩形，顺/逆时针）。</summary>
    private static List<Vector2> BuildPerimeterPath(Rect r, bool clockwise)
    {
        var corners = new List<Vector2>
        {
            new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
            new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
        };
        if (!clockwise) corners.Reverse();
        return corners;
    }

    private float chargePounceSpeed() => runSpeed * 1.5f;

    private static Sprite whiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            whiteSprite.name = "RT_MoonMarkWhite";
        }
        return whiteSprite;
    }

    private void OnDisable() => HideAllMarks();
}
