using UnityEngine;

/// <summary>
/// 攻击范围预警显示组件。纯视觉层（Presentation Layer）。
/// 使用运行时生成的 Mesh 显示圆形或扇形攻击范围；Mesh 不可用时回退到 SpriteRenderer。
/// 禁止：攻击逻辑、AI逻辑、状态机、计时器、事件、TryAttack、Enemy引用控制、Chase控制、Recovery控制。
/// </summary>
public class AttackIndicator : MonoBehaviour
{
    public enum ShapeType
    {
        Circle,
        Box,
        Sector,
        Line
    }

    [Header("渲染组件")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("颜色")]
    [SerializeField] private Color warningColor = new Color(1f, 1f, 0f, 120f / 255f);
    [SerializeField] private Color dangerColor = new Color(1f, 0f, 0f, 140f / 255f);

    [Header("形状")]
    [SerializeField] private ShapeType shape = ShapeType.Sector;

    [Header("显示行为")]
    [Tooltip("显示时脱离父物体、保持世界位置（敌人预警=true）；玩家近战范围显示=false（跟随玩家，v0.6.3）")]
    public bool detachOnShow = true;

    [Header("位置偏移")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Header("扇形细分")]
    [Tooltip("扇形每 5 度一个分段，角度越大分段越多")]
    [SerializeField] private float degreesPerSegment = 5f;

    [Tooltip("方向预警线的世界宽度。")]
    [SerializeField, Min(0.02f)] private float lineWidth = 0.16f;

    [Header("材质")]
    [SerializeField] private Material indicatorMaterial;

    private Mesh indicatorMesh;
    private Color currentColor;
    private bool useSpriteFallback;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private bool isDetached;

    private float currentRadius = 1f;
    private float currentAngle = 360f;
    private float currentWidth = 0.2f;
    private Vector2 currentDirection = Vector2.right;
    private float boxWidth = 0.2f;   // Box 形状的宽度（v0.6.3）

    public Color WarningColor => warningColor;
    public Color DangerColor => dangerColor;

    void Awake()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        InitializeRenderer();

        currentColor = warningColor;
        RebuildMesh();

        // 初始化完成后默认隐藏，由 Show()/Hide() 通过 renderer.enabled 控制，不再修改 GameObject active 状态。
        HideRenderers();
    }

    /// <summary>
    /// 初始化渲染器：优先 Mesh，失败则回退到 SpriteRenderer。
    /// </summary>
    private void InitializeRenderer()
    {
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (meshFilter != null && meshRenderer != null)
        {
            useSpriteFallback = false;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            if (indicatorMaterial == null)
                indicatorMaterial = new Material(shader);

            indicatorMaterial = new Material(indicatorMaterial);
            indicatorMaterial.shader = shader;
            indicatorMaterial.color = warningColor;
            indicatorMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            indicatorMaterial.SetInt("_ZWrite", 0);
            indicatorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            indicatorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            indicatorMaterial.renderQueue = 3000;

            meshRenderer.material = indicatorMaterial;
            meshRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            meshRenderer.sortingOrder = 100;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            indicatorMesh = new Mesh { name = "AttackIndicatorMesh" };
            meshFilter.mesh = indicatorMesh;

            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
        }
        else
        {
            useSpriteFallback = true;
            Debug.LogWarning("[AttackIndicator] MeshFilter/MeshRenderer 初始化失败，回退到 SpriteRenderer。", this);

            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = CreateCircleSprite();
                spriteRenderer.color = warningColor;
                spriteRenderer.sortingOrder = 100;
                spriteRenderer.enabled = false;
            }

            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }
    }

    void OnDisable()
    {
        HideRenderers();
        RestoreParent();
    }

    /// <summary>显示攻击范围指示器。</summary>
    public void Show()
    {
        // 显示时脱离父物体，保持当前世界位置与旋转，避免跟随 Enemy 移动/旋转。
        // detachOnShow = false（玩家近战范围显示）时保持父子关系，指示器跟随玩家（v0.6.3）。
        if (detachOnShow && !isDetached && transform.parent != null)
        {
            originalParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            transform.SetParent(null, true);
            isDetached = true;
        }

        // 脱离父物体后 ApplyTransform 不再重置缩放，避免覆盖 UpdateSpriteVisual 设置的半径。
        ApplyTransform();

        // 确保 Mesh/Sprite 与当前参数同步后再显示。
        RebuildMesh();

        ShowRenderers();
    }

    /// <summary>隐藏攻击范围指示器。</summary>
    public void Hide()
    {
        HideRenderers();
        RestoreParent();
    }

    /// <summary>
    /// 销毁指示器对象本体（v0.5.4.4.4）。所有者进入"即将销毁"流程时调用：
    /// 指示器显示期间脱离父物体挂在场景根，仅 Hide 会留下孤儿对象。
    /// </summary>
    public void DestroyIndicator()
    {
        Destroy(gameObject);
    }

    private void ShowRenderers()
    {
        if (meshRenderer != null)
            meshRenderer.enabled = true;
        else if (spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    private void HideRenderers()
    {
        if (meshRenderer != null)
            meshRenderer.enabled = false;
        else if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    /// <summary>设置攻击范围半径。</summary>
    public void SetRadius(float radius)
    {
        currentRadius = Mathf.Max(0.01f, radius);
        RebuildMesh();
    }

    /// <summary>设置攻击角度。扇形使用，圆形可忽略。</summary>
    public void SetAngle(float angle)
    {
        currentAngle = Mathf.Clamp(angle, 0f, 360f);
        RebuildMesh();
    }

    /// <summary>
    /// 设置矩形尺寸（v0.6.2）：length 为攻击方向延伸长度，width 为垂直宽度。
    /// 近战预警用——与 WeaponHitbox 的 OverlapBox 判定几何严格同源
    /// （v0.4.5.2 Final5"三者同源"原则：预警=动画=判定同一份数据）。
    /// </summary>
    public void SetBoxSize(float length, float width)
    {
        currentRadius = Mathf.Max(0.01f, length);
        currentWidth = Mathf.Max(0.01f, width);
        RebuildMesh();
    }

    /// <summary>设置攻击方向。扇形使用，圆形可忽略。</summary>
    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.0001f)
        {
            currentDirection = direction.normalized;
            RebuildMesh();
        }
    }

    /// <summary>设置当前颜色。</summary>
    public void SetColor(Color color)
    {
        currentColor = color;
        if (useSpriteFallback)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = currentColor;
        }
        else
        {
            if (indicatorMaterial != null)
                indicatorMaterial.color = currentColor;
        }
    }

    /// <summary>设置指示器形状。</summary>
    public void SetShape(ShapeType newShape)
    {
        shape = newShape;
        RebuildMesh();
    }

    /// <summary>
    /// 设置为矩形（Box，v0.6.3 枪矛戳击判定显示）：
    /// 从原点沿 currentDirection 伸出 length、宽 width 的矩形，实时跟随戳击伸展。
    /// </summary>
    public void SetBox(float length, float width)
    {
        shape = ShapeType.Box;
        currentRadius = Mathf.Max(0.01f, length);   // 复用 radius 字段存长度
        boxWidth = Mathf.Max(0.01f, width);
        RebuildMesh();
    }

    /// <summary>设置透明度（0~1）。</summary>
    public void SetAlpha(float alpha)
    {
        currentColor.a = Mathf.Clamp01(alpha);
        SetColor(currentColor);
    }

    private void RestoreParent()
    {
        if (!isDetached) return;

        // isDetached 为 true 时 Show() 必然缓存过 originalParent；此处为 fake-null
        // 说明父物体已被 Destroy。指示器已挂在场景根，不会随父销毁——必须自毁，
        // 否则 GameObject 与运行时 Mesh 将作为孤儿常驻场景、逐楼层累积（v0.5.4.4.4）。
        if (originalParent == null)
        {
            Destroy(gameObject);
            isDetached = false;
            return;
        }

        // 父物体停用中（敌人死亡流程、未进房休眠）：SetParent 会被 Unity 拒绝并报错。
        // 保持脱离状态与父引用，交由 Update 持续监视——父恢复激活则归位，
        // 父被销毁则自毁（如休眠敌人随楼层 Cleanup 一起回收的路径）。
        if (!originalParent.gameObject.activeInHierarchy)
            return;

        transform.SetParent(originalParent, false);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = Vector3.one;
        isDetached = false;
        originalParent = null;
    }

    void Update()
    {
        // 仅在"脱离父物体且归位被搁置"的窗口期做检查，正常状态零开销。
        if (!isDetached) return;
        if (originalParent == null) { Destroy(gameObject); return; }
        if (originalParent.gameObject.activeInHierarchy) RestoreParent();
    }

    void OnDestroy()
    {
        // 运行时创建的 Mesh/Material 不挂在场景对象上，Unity 不会自动回收。
        if (indicatorMesh != null) Destroy(indicatorMesh);
        if (indicatorMaterial != null) Destroy(indicatorMaterial);
    }

    private void ApplyTransform()
    {
        if (isDetached)
        {
            // 脱离父物体后重置旋转，让扇形方向完全由 currentDirection 决定，
            // 避免父物体旋转与扇形内部方向叠加导致双重旋转。
            transform.rotation = Quaternion.identity;
        }
        else
        {
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>根据当前形状重建 Mesh 或 Sprite。</summary>
    private void RebuildMesh()
    {
        // 如果 meshRenderer 不可用，使用 spriteRenderer 显示扇形/圆形。
        if (meshRenderer == null && spriteRenderer != null)
        {
            UpdateSpriteVisual();
            return;
        }

        switch (shape)
        {
            case ShapeType.Line:
                BuildLineMesh();
                break;
            case ShapeType.Box:
                BuildBoxMesh();
                break;
            case ShapeType.Sector:
                BuildSectorMesh();
                break;
            case ShapeType.Box:
                BuildBoxMesh();
                break;
            case ShapeType.Circle:
            default:
                BuildCircleMesh();
                break;
        }
    }

    /// <summary>矩形 Mesh（v0.6.3）：从原点沿 currentDirection 伸出 currentRadius 长、boxWidth 宽。</summary>
    private void BuildBoxMesh()
    {
        if (indicatorMesh == null) return;

        float halfW = boxWidth * 0.5f;
        Vector2 dir = currentDirection.normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * halfW;
        Vector2 tip = dir * currentRadius;

        indicatorMesh.Clear();
        indicatorMesh.vertices = new Vector3[]
        {
            -perp, perp, tip + perp, tip - perp
        };
        indicatorMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        indicatorMesh.RecalculateNormals();
        indicatorMesh.RecalculateBounds();
    }

    private void UpdateSpriteVisual()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = CreateSectorSprite(currentAngle, currentDirection);
        spriteRenderer.sortingOrder = 100;
        transform.localScale = new Vector3(currentRadius * 2f, currentRadius * 2f, 1f);
    }

    private Sprite CreateCircleSprite()
    {
        return CreateSectorSprite(360f, Vector2.right);
    }

    private Sprite CreateSectorSprite(float angle, Vector2 direction)
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size - 1, size - 1) * 0.5f;
        float radius = size * 0.5f - 1f;

        float halfAngle = angle * 0.5f;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float dist = offset.magnitude;

                if (dist > radius)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                if (angle < 360f)
                {
                    float pixelAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                    float delta = Mathf.DeltaAngle(baseAngle, pixelAngle);
                    if (Mathf.Abs(delta) > halfAngle)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                }

                pixels[y * size + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void BuildCircleMesh()
    {
        BuildSectorMeshInternal(360f, currentDirection);
    }

    private void BuildSectorMesh()
    {
        BuildSectorMeshInternal(currentAngle, currentDirection);
    }

    private void BuildLineMesh()
    {
        if (indicatorMesh == null) return;

        Vector2 direction = currentDirection.sqrMagnitude > 0.0001f
            ? currentDirection.normalized : Vector2.right;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (lineWidth * 0.5f);
        Vector2 end = direction * currentRadius;

        indicatorMesh.Clear();
        indicatorMesh.vertices = new[]
        {
            (Vector3)(-perpendicular),
            (Vector3)perpendicular,
            (Vector3)(end + perpendicular),
            (Vector3)(end - perpendicular)
        };
        indicatorMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        indicatorMesh.RecalculateNormals();
        indicatorMesh.RecalculateBounds();
    }

    /// <summary>
    /// 矩形 Mesh（v0.6.2）：从原点（攻击者中心）沿 currentDirection 延伸 currentRadius、
    /// 垂直宽 currentWidth。与 BuildLineMesh 同构（厚版预警线），顶点直接用世界方向计算、
    /// 不依赖自身 Transform 旋转（脱离父物体后 rotation 为 identity）。
    /// </summary>
    private void BuildBoxMesh()
    {
        if (indicatorMesh == null) return;

        Vector2 direction = currentDirection.sqrMagnitude > 0.0001f
            ? currentDirection.normalized : Vector2.right;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (currentWidth * 0.5f);
        Vector2 end = direction * currentRadius;

        indicatorMesh.Clear();
        indicatorMesh.vertices = new[]
        {
            (Vector3)(-perpendicular),
            (Vector3)perpendicular,
            (Vector3)(end + perpendicular),
            (Vector3)(end - perpendicular)
        };
        indicatorMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        indicatorMesh.RecalculateNormals();
        indicatorMesh.RecalculateBounds();
    }

    private void BuildSectorMeshInternal(float angle, Vector2 direction)
    {
        if (indicatorMesh == null) return;

        angle = Mathf.Clamp(angle, 0.01f, 360f);

        int segments = Mathf.Max(2, Mathf.RoundToInt(angle / Mathf.Max(0.1f, degreesPerSegment)));
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float halfAngle = angle * 0.5f;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentDegree = baseAngle - halfAngle + angle * t;
            float rad = currentDegree * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(rad) * currentRadius,
                Mathf.Sin(rad) * currentRadius,
                0f);
        }

        // 顶点顺序反转，确保在 2D 相机（从 Z 轴负方向看）下为正面
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        indicatorMesh.Clear();
        indicatorMesh.vertices = vertices;
        indicatorMesh.triangles = triangles;
        indicatorMesh.RecalculateNormals();
        indicatorMesh.RecalculateBounds();
    }
}
