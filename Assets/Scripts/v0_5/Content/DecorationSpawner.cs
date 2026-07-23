using UnityEngine;

/// <summary>
/// 装饰生成器（静态）：放纯装饰（无碰撞，sortingOrder 低于角色）。
/// 随机 Z 旋转 + 非等比缩放抖动 + 颜色深浅抖动打破重复感（全部走房间子 seed，同 seed 可复现）；
/// 装饰无任何逻辑，生成后即与系统脱钩。
/// </summary>
public static class DecorationSpawner
{
    // 抖动范围：缩放每轴 ×0.7~1.4，颜色亮度 ×0.85~1.15（alpha 不动）
    private const float ScaleJitterMin = 0.7f, ScaleJitterMax = 1.4f;
    private const float ColorJitterMin = 0.85f, ColorJitterMax = 1.15f;

    public static void Spawn(Room room, SpawnTable table, System.Random rng)
    {
        if (room == null || table == null) return;

        int count = table.RollCount(rng);
        for (int i = 0; i < count; i++)
        {
            SpawnTable.Entry e = table.PickEntry(rng);
            if (e == null) continue;
            if (!SpawnPositionHelper.TryFind(room, rng, out Vector3 pos)) continue;

            Quaternion rot = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 360.0));
            GameObject go = Object.Instantiate(e.prefab, pos, rot, room.ContentRoot);
            go.name = $"{e.prefab.name}_{room.Id}_{i}";

            // 非等比缩放抖动：同一 prefab 呈现不同轮廓
            Vector3 baseScale = go.transform.localScale;
            go.transform.localScale = new Vector3(
                baseScale.x * Jitter(rng, ScaleJitterMin, ScaleJitterMax),
                baseScale.y * Jitter(rng, ScaleJitterMin, ScaleJitterMax),
                baseScale.z);

            // 颜色深浅抖动：同一 prefab 呈现不同深浅
            if (go.TryGetComponent(out SpriteRenderer sr))
            {
                float k = Jitter(rng, ColorJitterMin, ColorJitterMax);
                Color c = sr.color;
                sr.color = new Color(
                    Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
            }
        }
    }

    private static float Jitter(System.Random rng, float min, float max)
        => Mathf.Lerp(min, max, (float)rng.NextDouble());
}
