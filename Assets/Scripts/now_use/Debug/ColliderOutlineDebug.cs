#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;   // TilemapCollider2D/CompositeCollider2D（墙体轮廓过滤用）

/// <summary>
/// 碰撞体运行时可视化（编辑器专用，打包自动剔除）。
/// Game/Scene 视图实时描绘：红=实体碰撞体（攻击判定的真实受击层）、
/// 黄=触发器（逻辑探测层，WeaponHitbox 判定一律跳过）、橙=挥击中的武器判定盒。
/// 用途：排查"看得见接触却不受击"类判定错位（v1.1.1 玩家上半身不受击诊断）。
///
/// 约定：
/// - 纯表现层只读工具：不改任何战斗/物理逻辑，几何消费 WeaponHitbox.TryGetSwingBox（同源，不自算第二套）。
/// - RuntimeInitializeOnLoadMethod 自装到隐藏常驻对象：不改场景、不改 Prefab（R1/R4 无关），
///   不产生可序列化的 Debug_ 残留；删除本文件即完全移除。
/// - F9 开关（v0.7.5 键位表外，无冲突）；墙体 Tilemap/Composite 碰撞体线条噪音过大不画。
/// - 重扫每 0.5s 一次（敌人/障碍动态增删），重扫外每帧绘制零 GC（静态 scratch 复用）。
/// </summary>
public class ColliderOutlineDebug : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;
    private const int CircleSegments = 24;
    private const int ArcSegments = 12;
    private const float DrawZOffset = -0.5f;   // 线画在实体所在 z 前方，避免被遮挡

    private static readonly Color SolidColor = new Color(1f, 0.15f, 0.15f);    // 实体碰撞（可受击）
    private static readonly Color TriggerColor = new Color(1f, 0.85f, 0.2f);  // 触发器（攻击忽略）
    private static readonly Color SwingColor = new Color(1f, 0.5f, 0f);       // 挥击判定盒

    private static readonly Vector2[] scratch = new Vector2[64];

    private readonly List<Collider2D> colliders = new List<Collider2D>(64);
    private readonly List<string> labels = new List<string>(64);       // 与 colliders 平行；null=不标名
    private readonly List<WeaponHitbox> hitboxes = new List<WeaponHitbox>(16);

    private float refreshTimer;
    private bool show = true;
    private Material lineMat;
    private static GUIStyle legendStyle;
    private static GUIStyle labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("ColliderOutlineDebug(Runtime)");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<ColliderOutlineDebug>();
        Debug.Log("[ColliderOutlineDebug] 已启用：红=实体碰撞(受击层) 黄=触发器(攻击忽略) 橙=挥击判定盒 | F9 开关");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            show = !show;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = RefreshInterval;
            RefreshCache();
        }
    }

    private void RefreshCache()
    {
        colliders.Clear();
        labels.Clear();
        hitboxes.Clear();

        var found = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        foreach (var c in found)
        {
            if (c is TilemapCollider2D || c is CompositeCollider2D) continue;   // 墙体轮廓噪音过大
            colliders.Add(c);
            labels.Add(BuildLabel(c));
        }

        var boxes = FindObjectsByType<WeaponHitbox>(FindObjectsInactive.Exclude);
        hitboxes.AddRange(boxes);
    }

    private static string BuildLabel(Collider2D c)
    {
        string n = c.gameObject.name;
        if (!(n.StartsWith("Player") || n.StartsWith("Enemy") || n.StartsWith("Boss"))) return null;
        string tag = c.isTrigger ? "触发" : "实体";
        string type = c is CapsuleCollider2D ? "胶囊"
            : c is BoxCollider2D ? "盒"
            : c is CircleCollider2D ? "圆"
            : "多边";
        return $"{n}·{type}{tag}";
    }

    void OnRenderObject()
    {
        if (!show) return;
        Camera cam = Camera.current;
        if (cam == null) return;

        if (lineMat == null)
        {
            lineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
            lineMat.hideFlags = HideFlags.HideAndDontSave;
        }

        lineMat.SetPass(0);
        GL.PushMatrix();
        GL.modelview = cam.worldToCameraMatrix;
        GL.LoadProjectionMatrix(cam.projectionMatrix);
        GL.Begin(GL.LINES);

        for (int i = 0; i < colliders.Count; i++)
        {
            var c = colliders[i];
            if (c == null) continue;
            DrawCollider(c, c.isTrigger ? TriggerColor : SolidColor);
        }

        for (int i = 0; i < hitboxes.Count; i++)
        {
            var hb = hitboxes[i];
            if (hb == null) continue;
            if (hb.TryGetSwingBox(out Vector2 center, out Vector2 size, out float angle))
                DrawRotatedBox(center, size, angle, SwingColor, hb.transform.position.z + DrawZOffset);
        }

        GL.End();
        GL.PopMatrix();
    }

    private static void DrawCollider(Collider2D c, Color col)
    {
        GL.Color(col);
        switch (c)
        {
            case BoxCollider2D box:
            {
                Vector2 h = box.size * 0.5f;
                Vector2 o = box.offset;
                scratch[0] = o + new Vector2(-h.x, -h.y);
                scratch[1] = o + new Vector2(h.x, -h.y);
                scratch[2] = o + new Vector2(h.x, h.y);
                scratch[3] = o + new Vector2(-h.x, h.y);
                EmitLocalPath(scratch, 4, true, c.transform);
                break;
            }
            case CircleCollider2D cir:
            {
                Transform t = c.transform;
                float scale = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
                Vector2 center = t.TransformPoint(cir.offset);
                float r = cir.radius * scale;
                float z = t.position.z + DrawZOffset;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float a0 = i / (float)CircleSegments * Mathf.PI * 2f;
                    float a1 = (i + 1) / (float)CircleSegments * Mathf.PI * 2f;
                    GL.Vertex(new Vector3(center.x + Mathf.Cos(a0) * r, center.y + Mathf.Sin(a0) * r, z));
                    GL.Vertex(new Vector3(center.x + Mathf.Cos(a1) * r, center.y + Mathf.Sin(a1) * r, z));
                }
                break;
            }
            case CapsuleCollider2D cap:
            {
                Vector2 h = cap.size * 0.5f;
                Vector2 o = cap.offset;
                float r = Mathf.Min(h.x, h.y);
                int n = 0;
                if (cap.direction == CapsuleDirection2D.Vertical)
                {
                    Vector2 top = o + new Vector2(0f, h.y - r);
                    Vector2 bottom = o + new Vector2(0f, -(h.y - r));
                    for (int i = 0; i <= ArcSegments; i++)
                    {
                        float a = (180f + 180f * i / ArcSegments) * Mathf.Deg2Rad;
                        scratch[n++] = bottom + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    for (int i = 0; i <= ArcSegments; i++)
                    {
                        float a = 180f * i / ArcSegments * Mathf.Deg2Rad;
                        scratch[n++] = top + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                }
                else
                {
                    Vector2 right = o + new Vector2(h.x - r, 0f);
                    Vector2 left = o + new Vector2(-(h.x - r), 0f);
                    for (int i = 0; i <= ArcSegments; i++)
                    {
                        float a = (-90f + 180f * i / ArcSegments) * Mathf.Deg2Rad;
                        scratch[n++] = right + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    for (int i = 0; i <= ArcSegments; i++)
                    {
                        float a = (90f + 180f * i / ArcSegments) * Mathf.Deg2Rad;
                        scratch[n++] = left + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                }
                EmitLocalPath(scratch, n, true, c.transform);
                break;
            }
            case PolygonCollider2D poly:
                EmitLocalPath(poly.points, true, c.transform);
                break;
            case EdgeCollider2D edge:
                EmitLocalPath(edge.points, false, c.transform);
                break;
        }
    }

    private static void EmitLocalPath(Vector2[] pts, bool close, Transform t)
        => EmitLocalPath(pts, pts.Length, close, t);

    private static void EmitLocalPath(Vector2[] pts, int count, bool close, Transform t)
    {
        float z = t.position.z + DrawZOffset;
        int segments = close ? count : count - 1;
        for (int i = 0; i < segments; i++)
        {
            Vector3 a = t.TransformPoint(pts[i]);
            Vector3 b = t.TransformPoint(pts[(i + 1) % count]);
            GL.Vertex(new Vector3(a.x, a.y, z));
            GL.Vertex(new Vector3(b.x, b.y, z));
        }
    }

    private static void DrawRotatedBox(Vector2 center, Vector2 size, float angle, Color col, float z)
    {
        GL.Color(col);
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        Vector2 h = size * 0.5f;
        scratch[0] = new Vector2(-h.x, -h.y);
        scratch[1] = new Vector2(h.x, -h.y);
        scratch[2] = new Vector2(h.x, h.y);
        scratch[3] = new Vector2(-h.x, h.y);
        for (int i = 0; i < 4; i++)
            scratch[i] = center + new Vector2(scratch[i].x * cos - scratch[i].y * sin, scratch[i].x * sin + scratch[i].y * cos);
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = scratch[i];
            Vector2 b = scratch[(i + 1) % 4];
            GL.Vertex(new Vector3(a.x, a.y, z));
            GL.Vertex(new Vector3(b.x, b.y, z));
        }
    }

    void OnGUI()
    {
        if (!show) return;
        if (legendStyle == null)
        {
            legendStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            legendStyle.normal.textColor = Color.white;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            labelStyle.normal.textColor = new Color(1f, 0.6f, 0.6f);
        }
        GUI.Label(new Rect(12f, 10f, 1000f, 22f),
            "碰撞体可视化：红=实体碰撞(受击层) 黄=触发器(攻击忽略) 橙=挥击判定盒 | F9 开关", legendStyle);

        Camera cam = Camera.main;
        if (cam == null) return;
        for (int i = 0; i < colliders.Count; i++)
        {
            var c = colliders[i];
            if (c == null || labels[i] == null) continue;
            Bounds b = c.bounds;
            Vector3 sp = cam.WorldToScreenPoint(b.center + Vector3.up * (b.extents.y + 0.25f));
            if (sp.z <= 0f) continue;
            GUI.Label(new Rect(sp.x - 90f, Screen.height - sp.y - 12f, 180f, 20f), labels[i], labelStyle);
        }
    }
}
#endif
