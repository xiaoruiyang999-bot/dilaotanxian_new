using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class RoadMaskGroundSurface : MonoBehaviour
{
    private const int MaskPixelsPerCell = 8;
    private const float BaseEdgeWidth = 0.10f;
    private const float EdgeNoiseFrequency = 1.35f;
    private const float EdgeNoiseAmplitude = 0.07f;
    private const string ShaderName = "Dungeon/RoadMaskGround";
    private const string GrassResource = "Art/Tiles/Ground/grass_base";
    private const string DirtResource = "Art/Tiles/Ground/dirt_base";
    private Mesh generatedMesh;
    private Material generatedMaterial;
    private Texture2D generatedMask;

    public bool Build(TerrainMask terrain, IReadOnlyCollection<Vector2Int> visibleCells, TilemapRenderer referenceRenderer, int seed)
    {
        if (terrain == null || visibleCells == null || visibleCells.Count == 0) return false;
        Texture2D grass = Resources.Load<Texture2D>(GrassResource);
        Texture2D dirt = Resources.Load<Texture2D>(DirtResource);
        Shader shader = Shader.Find(ShaderName);
        if (grass == null || dirt == null || shader == null)
        {
            Debug.LogError($"[RoadMask] 缺少渲染资源：grass={grass != null}, dirt={dirt != null}, shader={shader != null}");
            return false;
        }
        var cellSet = visibleCells as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(visibleCells);
        RectInt bounds = ComputeBounds(cellSet);
        generatedMask = BuildMask(terrain, cellSet, bounds, seed);
        generatedMesh = BuildQuad(bounds);
        generatedMaterial = new Material(shader) { name = "RoadMaskGround_Runtime" };
        generatedMaterial.SetTexture("_GrassTex", grass);
        generatedMaterial.SetTexture("_DirtTex", dirt);
        generatedMaterial.SetTexture("_MaskTex", generatedMask);
        generatedMaterial.SetFloat("_TextureScale", 1f);
        var meshFilter = gameObject.AddComponent<MeshFilter>();
        var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = generatedMesh;
        meshRenderer.sharedMaterial = generatedMaterial;
        if (referenceRenderer != null)
        {
            meshRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
            meshRenderer.sortingOrder = referenceRenderer.sortingOrder;
        }
        return true;
    }

    private static Texture2D BuildMask(TerrainMask terrain, HashSet<Vector2Int> visibleCells, RectInt bounds, int seed)
    {
        int width = bounds.width * MaskPixelsPerCell;
        int height = bounds.height * MaskPixelsPerCell;
        var pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
        {
            float worldY = bounds.yMin + (py + 0.5f) / MaskPixelsPerCell;
            int cellY = Mathf.FloorToInt(worldY);
            for (int px = 0; px < width; px++)
            {
                float worldX = bounds.xMin + (px + 0.5f) / MaskPixelsPerCell;
                int cellX = Mathf.FloorToInt(worldX);
                if (!visibleCells.Contains(new Vector2Int(cellX, cellY))) continue;
                bool road = IsInsideRoad(terrain, worldX, worldY, cellX, cellY, seed);
                pixels[py * width + px] = new Color32(road ? (byte)255 : (byte)0, 255, 0, 255);
            }
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "RoadMask_Runtime",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static bool IsInsideRoad(TerrainMask terrain, float worldX, float worldY, int cellX, int cellY, int seed)
    {
        // 先合并完整逻辑土格：道路内部永远实心，噪声不得在相邻土格之间打洞。
        if (terrain.IsDirt(cellX, cellY)) return true;

        float nearestBoundary = float.MaxValue;
        for (int oy = -1; oy <= 1; oy++)
        for (int ox = -1; ox <= 1; ox++)
        {
            int dirtX = cellX + ox;
            int dirtY = cellY + oy;
            if (!terrain.IsDirt(dirtX, dirtY)) continue;

            // 到“所有土格合并区域”的距离，而不是到每个土格中心圆的距离。
            float dx = Mathf.Max(Mathf.Abs(worldX - (dirtX + 0.5f)) - 0.5f, 0f);
            float dy = Mathf.Max(Mathf.Abs(worldY - (dirtY + 0.5f)) - 0.5f, 0f);
            nearestBoundary = Mathf.Min(nearestBoundary, Mathf.Sqrt(dx * dx + dy * dy));
        }
        if (nearestBoundary == float.MaxValue) return false;

        // 连续世界噪声只改变整体道路的外轮廓，不作用于道路内部。
        float noise = SampleCoherentNoise(worldX * EdgeNoiseFrequency, worldY * EdgeNoiseFrequency, seed);
        float edgeWidth = Mathf.Max(0.02f, BaseEdgeWidth + (noise * 2f - 1f) * EdgeNoiseAmplitude);
        return nearestBoundary <= edgeWidth;
    }

    private static float SampleCoherentNoise(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = Smooth(x - x0);
        float ty = Smooth(y - y0);
        float a = TerrainMask.Hash01(x0, y0, seed);
        float b = TerrainMask.Hash01(x0 + 1, y0, seed);
        float c = TerrainMask.Hash01(x0, y0 + 1, seed);
        float d = TerrainMask.Hash01(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
    private static RectInt ComputeBounds(HashSet<Vector2Int> cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int cell in cells)
        {
            minX = Mathf.Min(minX, cell.x); minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x + 1); maxY = Mathf.Max(maxY, cell.y + 1);
        }
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private static Mesh BuildQuad(RectInt bounds)
    {
        var mesh = new Mesh { name = "RoadMaskGround_Runtime" };
        mesh.vertices = new[]
        {
            new Vector3(bounds.xMin, bounds.yMin, 0f), new Vector3(bounds.xMax, bounds.yMin, 0f),
            new Vector3(bounds.xMax, bounds.yMax, 0f), new Vector3(bounds.xMin, bounds.yMax, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        DestroyOwned(generatedMesh);
        DestroyOwned(generatedMaterial);
        DestroyOwned(generatedMask);
    }

    private static void DestroyOwned(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}