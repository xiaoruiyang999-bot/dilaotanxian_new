using System.Collections;
using UnityEngine;

public class WarriorAttack : MonoBehaviour, IAttack
{
    [Header("攻击属性")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackAngle = 90f; // 扇形角度（全角）
    [SerializeField] private LayerMask enemyLayer;

    [Header("视觉指示器")]
    [SerializeField] private Sprite attackIndicatorSprite; // 已弃用：常态范围改为动态 Mesh 实心圆
    [SerializeField] private float indicatorDuration = 0.2f;
    [SerializeField] private int fanSegments = 20;         // 扇形网格分段数
    [SerializeField] private int circleSegments = 40;      // 圆形网格分段数

    public float AttackRange => attackRange;
    public float AttackDamage => attackDamage;

    private LineRenderer rangeIndicator; // 常态白圈（空心圆环）

    void Awake()
    {
        if (enemyLayer == 0)
            enemyLayer = LayerMask.GetMask("Enemy");

        CreateRangeIndicator();
    }

    void OnDestroy()
    {
        if (rangeIndicator != null)
            Destroy(rangeIndicator.gameObject);
    }

    public void Execute()
    {
        StartCoroutine(AttackCoroutine());
    }

    private IEnumerator AttackCoroutine()
    {
        // 隐藏常态白圈
        if (rangeIndicator != null)
            rangeIndicator.enabled = false;

        // 显示红色扇形打击范围
        GameObject fanIndicator = CreateFanIndicator();

        // 执行实际伤害判定
        PerformFanAttack();

        yield return new WaitForSeconds(indicatorDuration);

        if (fanIndicator != null)
            Destroy(fanIndicator);

        // 恢复常态白圈
        if (rangeIndicator != null)
            rangeIndicator.enabled = true;
    }

    private void PerformFanAttack()
    {
        // 1. 获取圆形范围内所有敌人
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position, attackRange, enemyLayer);

        // 2. 获取角色面向方向（父对象Player的transform.right）
        Vector2 facingDir = transform.parent != null
            ? transform.parent.right
            : Vector2.right;

        float halfAngle = attackAngle * 0.5f;

        foreach (Collider2D hit in hits)
        {
            // 忽略触发器（如敌人的探测圈 CircleCollider2D），只命中实际物理碰撞体
            if (hit.isTrigger) continue;

            // 使用碰撞体上距离玩家最近的点进行判定，确保是 BoxCollider2D 本体进入攻击范围才造成伤害
            Vector2 closestPoint = hit.ClosestPoint(transform.position);
            Vector2 toPoint = closestPoint - (Vector2)transform.position;
            float distance = toPoint.magnitude;
            if (distance > attackRange) continue;

            // 3. 计算最近点与角色面向方向的夹角
            float angle = Vector2.Angle(facingDir, toPoint.normalized);

            // 4. 夹角在扇形半角内则命中
            if (angle <= halfAngle)
            {
                if (hit.TryGetComponent<IDamageable>(out IDamageable dmg))
                {
                    dmg.TakeDamage(attackDamage);
                }
            }
        }
    }

    /// <summary>
    /// 创建常态白圈指示器，使用 LineRenderer 绘制空心圆环
    /// </summary>
    private void CreateRangeIndicator()
    {
        GameObject indicator = new GameObject("RangeIndicator");
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = Vector3.zero;
        indicator.transform.localRotation = Quaternion.identity;

        rangeIndicator = indicator.AddComponent<LineRenderer>();
        rangeIndicator.useWorldSpace = false;
        rangeIndicator.loop = true;
        rangeIndicator.startWidth = 0.03f;
        rangeIndicator.endWidth = 0.03f;
        rangeIndicator.startColor = new Color(1f, 1f, 1f, 0.6f);
        rangeIndicator.endColor = new Color(1f, 1f, 1f, 0.6f);
        rangeIndicator.sortingOrder = 4;

        Shader lineShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (lineShader == null)
            lineShader = Shader.Find("Sprites/Default");
        rangeIndicator.material = new Material(lineShader);

        BuildCircleLine(rangeIndicator, attackRange, circleSegments);
    }

    /// <summary>
    /// 用 LineRenderer 绘制空心圆
    /// </summary>
    private void BuildCircleLine(LineRenderer line, float radius, int segments)
    {
        line.positionCount = segments + 1;
        float step = 360f * Mathf.Deg2Rad / segments;

        for (int i = 0; i <= segments; i++)
        {
            float rad = i * step;
            float x = Mathf.Cos(rad) * radius;
            float y = Mathf.Sin(rad) * radius;
            line.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    /// <summary>
    /// 创建红色扇形打击范围指示器
    /// </summary>
    private GameObject CreateFanIndicator()
    {
        GameObject indicator = new GameObject("AttackFanIndicator");
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = Vector3.zero;
        indicator.transform.localRotation = Quaternion.identity;

        // 使用 MeshRenderer + 动态 Mesh 绘制扇形
        MeshFilter meshFilter = indicator.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = indicator.AddComponent<MeshRenderer>();
        Shader fanShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (fanShader == null)
            fanShader = Shader.Find("Sprites/Default");
        meshRenderer.material = new Material(fanShader);
        meshRenderer.material.color = new Color(1f, 0f, 0f, 0.35f);
        meshRenderer.sortingOrder = 6;

        meshFilter.mesh = BuildFanMesh(attackRange, attackAngle, fanSegments);

        // 扇形本地朝向：默认指向 +X，跟随父对象旋转即可
        return indicator;
    }

    /// <summary>
    /// 生成以 (0,0) 为顶点、指向 +X 的扇形 Mesh
    /// </summary>
    private Mesh BuildFanMesh(float radius, float angle, int segments)
    {
        Mesh mesh = new Mesh();
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        Vector2[] uvs = new Vector2[segments + 2];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float rad = -halfAngle + t * (halfAngle * 2f);

            float x = Mathf.Cos(rad) * radius;
            float y = Mathf.Sin(rad) * radius;

            vertices[i + 1] = new Vector3(x, y, 0f);
            uvs[i + 1] = new Vector2(
                0.5f + Mathf.Cos(rad) * 0.5f,
                0.5f + Mathf.Sin(rad) * 0.5f);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    void OnDrawGizmosSelected()
    {
        // 在Scene视图中显示扇形攻击范围
        Vector2 origin = transform.position;
        Vector2 facingDir = transform.parent != null ? transform.parent.right : Vector2.right;
        float halfAngle = attackAngle * 0.5f;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(origin, attackRange);

        // 绘制扇形边界线
        Vector2 leftDir = Quaternion.Euler(0, 0, halfAngle) * facingDir;
        Vector2 rightDir = Quaternion.Euler(0, 0, -halfAngle) * facingDir;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + leftDir * attackRange);
        Gizmos.DrawLine(origin, origin + rightDir * attackRange);
    }
}
