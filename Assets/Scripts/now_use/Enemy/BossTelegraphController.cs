using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.10 批1 Boss 预警控制器（文档 §八）：圆形落点预警（三重跃击）。
/// 批2 扩展直线锁定（奔袭）与序列地面（月痕）。程序化圆形（占用现成 AttackIndicator
/// 的成本高于自绘——Boss 预警脱离父物体逻辑与普通敌人共用会互相干扰）：
/// 落点半透明圆盘，锁定位置=实际判定位置（同源由调用方保证）。
/// 生命周期：HideAll 收口（死亡/取消/阶段转换由 Brain 调用）。
/// </summary>
public class BossTelegraphController : MonoBehaviour
{
    private static readonly Color WarningColor = new Color(1f, 0.35f, 0.25f, 0.30f);
    private readonly List<GameObject> active = new List<GameObject>();

    /// <summary>圆形落点预警：duration 后自动隐藏（攻击落地时调用方再 Hide 提前收）。</summary>
    /// <summary>经 Brain 统一广播（美术只订 OnWarningShown 一个源）。</summary>
    private GrandBossBrain brainCache;
    internal void WireBrain(GrandBossBrain brain) => brainCache = brain;

    public void ShowCircle(Vector2 center, float radius, float duration)
    {
        if (brainCache != null) brainCache.BroadcastWarning(center, radius);
        var go = new GameObject("BossTelegraph_Circle");
        go.transform.SetParent(null, false);   // P0-7:预警挂场景根不挂 Boss(锁点后不随 Boss 移动)
        go.transform.position = center;
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        Mesh mesh = BuildCircleMesh(radius);
        mf.sharedMesh = mesh;
        mr.material = BuildMaterial();

        active.Add(go);
        if (duration > 0f) StartCoroutine(AutoHide(go, duration));
    }

    /// <summary>直线锁定预警（奔袭/扑击）：起点+方向+长度；duration 后自动隐藏。</summary>
    public void ShowLine(Vector2 start, Vector2 dir, float length, float duration)
    {
        if (brainCache != null) brainCache.BroadcastWarning(start, length);

        var go = new GameObject("BossTelegraph_Line");
        go.transform.SetParent(null, false);   // P0-7:预警挂场景根不挂 Boss(锁点后不随 Boss 移动)
        go.transform.position = start + dir * (length * 0.5f);
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(1f, 0.45f, 0.25f, 0.35f);
        sr.sortingOrder = 1;
        go.transform.localScale = new Vector3(length, 1.2f, 1f);

        active.Add(go);
        if (duration > 0f) StartCoroutine(AutoHide(go, duration));
    }

    private static Sprite ws;
    private static Sprite CreateWhiteSprite()
    {
        if (ws == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            ws = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
            ws.name = "RT_TelegraphWhite";
        }
        return ws;
    }

    public void HideAll()
    {
        foreach (GameObject go in active) if (go != null) Destroy(go);
        active.Clear();
    }

    private System.Collections.IEnumerator AutoHide(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null) { active.Remove(go); Destroy(go); }
    }

    private static Mesh BuildCircleMesh(float radius)
    {
        int seg = Mathf.Max(12, (int)radius * 12);
        var vertices = new Vector3[seg + 1];
        vertices[0] = Vector3.zero;
        var triangles = new int[seg * 3];
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2 <= seg ? i + 2 : 1;
        }
        var mesh = new Mesh { name = "RT_TelegraphCircle" };   // v1.1.40：运行时资产命名
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        return mesh;
    }

    private static Material matCache;
    private static Material BuildMaterial()
    {
        if (matCache != null) return matCache;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        matCache = new Material(shader) { color = WarningColor };
        return matCache;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        HideAll();
    }
}
