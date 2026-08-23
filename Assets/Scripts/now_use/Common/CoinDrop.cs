using UnityEngine;

/// <summary>
/// 金币掉落物（M2·v0.7.0，v0.8 重写为纯运动学）：敌人死亡散落的金币。
/// 无刚体无碰撞体（不参与物理世界、不弹跳、不被墙挡）——散落与磁吸全部代码驱动：
/// 随机方向滑散减速停下 → 靠近玩家磁吸加速 → 触碰入账。
/// 零资产纯代码（程序员美术）：金色小圆 = 程序生成 Sprite，静态缓存共享。
/// </summary>
public class CoinDrop : MonoBehaviour
{
    public static readonly Color CoinColor = new Color(1f, 0.82f, 0.25f);

    private const float MagnetRange = 1.95f;     // 磁吸半径
    private const float PickupRange = 0.45f;    // 拾取半径
    private const float MagnetSpeed = 13.5f;
    private const float Lifetime = 12f;
    private const float SlideDamping = 0.90f;   // 滑散速度每帧衰减（帧率无关近似用 Time 系）

    private static Sprite coinSprite;

    private Transform player;
    private SpriteRenderer sr;
    private Vector2 velocity;      // 滑散速度（代码积分）
    private float spin;            // 旋转速度
    private float age;
    private bool magnetized;

    /// <summary>共享的金币 Sprite（32px 程序绘制圆，CoinHUD 复用同款视觉）。</summary>
    public static Sprite CoinSprite
    {
        get
        {
            if (coinSprite == null)
            {
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                float r = size * 0.4f, c = size * 0.5f - 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        // 边缘一圈深色描边，中心亮色，程序员美术的"硬币感"
                        Color col = d > r - 2f ? new Color(0.75f, 0.55f, 0.1f)
                            : d > r - 4f ? new Color(1f, 0.9f, 0.5f) : CoinColor;
                        tex.SetPixel(x, y, d <= r ? col : Color.clear);
                    }
                tex.Apply();
                coinSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            }
            return coinSprite;
        }
    }

    /// <summary>在 pos 处散落 count 枚金币（EnemyController.OnEnemyDeath / 宝箱共用入口）。</summary>
    public static void Spawn(Vector3 position, int count)
    {
        if (count <= 0) return;
        // 挂 dungeonRoot 下随楼层清理（DungeonBuilder.Cleanup 统一销毁）；找不到就挂场景根
        Transform parent = null;
        var root = GameObject.Find("DungeonRoot");
        if (root != null) parent = root.transform;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<CoinDrop>();
        }
    }

    void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CoinSprite;
        sr.sortingOrder = 4;   // 狼人(2)/武器(3)之上，血条(10)之下
        transform.localScale = Vector3.one * 0.28f;

        // 随机方向滑散（纯运动学，无物理组件）
        velocity = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f);
        spin = Random.Range(-400f, 400f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }
        if (age > Lifetime - 2f)
            sr.enabled = (Time.time * 8f) % 1f > 0.4f;   // 最后 2s 闪烁

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist < PickupRange)
        {
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null) stats.AddCoins(1);
            AudioManager.PlaySFX("coin");
            Destroy(gameObject);
            return;
        }

        if (dist < MagnetRange)
        {
            // 磁吸：朝玩家加速飞行（越近越快，贴脸吸走）
            magnetized = true;
            Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
            velocity = Vector2.Lerp(velocity, dir * MagnetSpeed, 20f * dt);
        }
        else if (magnetized && dist >= MagnetRange + 0.3f)
        {
            magnetized = false;   // 玩家离开磁吸圈：速度自然衰减停住
        }

        // 滑散减速（磁吸时保留高速）
        if (!magnetized)
            velocity *= Mathf.Pow(SlideDamping, dt * 60f);   // 帧率无关衰减
        transform.position += (Vector3)(velocity * dt);
        transform.Rotate(0f, 0f, spin * dt);
        spin *= Mathf.Pow(0.95f, dt * 60f);
    }
}
