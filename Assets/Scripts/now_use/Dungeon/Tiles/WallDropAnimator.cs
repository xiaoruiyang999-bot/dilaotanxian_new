using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 墙体入场表现（v1.1.43）：复制最终 Walls 到一张无 Collider 的临时 Tilemap，
/// 按格子确定性错峰从上方坠落；真实墙体只隐藏 Renderer，碰撞始终处于最终位置。
/// 动画结束后恢复真实 Renderer 并销毁临时视觉层。
/// </summary>
public sealed class WallDropAnimator : MonoBehaviour
{
    private sealed class FallingCell
    {
        public Vector3Int Position;
        public Matrix4x4 FinalMatrix;
        public float Delay;
        public bool Landed;
    }

    private readonly List<FallingCell> cells = new List<FallingCell>(512);
    private Tilemap source;
    private TilemapRenderer sourceRenderer;
    private bool sourceRendererWasEnabled;
    private GameObject visualObject;
    private Tilemap visualTilemap;
    private Coroutine routine;

    public void Play(Tilemap sourceTilemap, int seed, float height, float fallDuration, float staggerDuration)
    {
        Cancel();
        if (sourceTilemap == null) return;

        source = sourceTilemap;
        sourceRenderer = source.GetComponent<TilemapRenderer>();
        if (sourceRenderer == null || !sourceRenderer.enabled) return;

        BuildVisualLayer(seed, Mathf.Max(0f, height), Mathf.Max(0.01f, staggerDuration));
        if (cells.Count == 0)
        {
            DestroyVisualLayer();
            return;
        }

        sourceRendererWasEnabled = sourceRenderer.enabled;
        sourceRenderer.enabled = false;
        routine = StartCoroutine(Animate(Mathf.Max(0.01f, height),
            Mathf.Max(0.01f, fallDuration), Mathf.Max(0f, staggerDuration)));
    }

    public void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        RestoreSourceRenderer();
        DestroyVisualLayer();
        cells.Clear();
    }

    private void BuildVisualLayer(int seed, float height, float staggerDuration)
    {
        visualObject = new GameObject("WallDropVisual", typeof(Tilemap), typeof(TilemapRenderer));
        Transform visualTransform = visualObject.transform;
        visualTransform.SetParent(source.transform.parent, false);
        visualTransform.localPosition = source.transform.localPosition;
        visualTransform.localRotation = source.transform.localRotation;
        visualTransform.localScale = source.transform.localScale;

        visualTilemap = visualObject.GetComponent<Tilemap>();
        visualTilemap.color = source.color;
        visualTilemap.tileAnchor = source.tileAnchor;

        TilemapRenderer visualRenderer = visualObject.GetComponent<TilemapRenderer>();
        visualRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        visualRenderer.sortingOrder = sourceRenderer.sortingOrder;
        visualRenderer.sortOrder = sourceRenderer.sortOrder;
        visualRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

        BoundsInt bounds = source.cellBounds;
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase tile = source.GetTile(position);
            if (tile == null) continue;

            Matrix4x4 finalMatrix = source.GetTransformMatrix(position);
            float delay = Hash01(position, seed) * staggerDuration;
            cells.Add(new FallingCell
            {
                Position = position,
                FinalMatrix = finalMatrix,
                Delay = delay
            });

            visualTilemap.SetTile(position, tile);
            visualTilemap.SetTileFlags(position, TileFlags.None);
            visualTilemap.SetColor(position, source.GetColor(position));
            visualTilemap.SetTransformMatrix(position,
                Matrix4x4.Translate(Vector3.up * height) * finalMatrix);
        }
    }

    private IEnumerator Animate(float height, float fallDuration, float staggerDuration)
    {
        float elapsed = 0f;
        float totalDuration = fallDuration + staggerDuration;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < cells.Count; i++)
            {
                FallingCell cell = cells[i];
                if (cell.Landed || elapsed < cell.Delay) continue;

                float t = Mathf.Clamp01((elapsed - cell.Delay) / fallDuration);
                float offset = height * (1f - EaseOutBounce(t));
                visualTilemap.SetTransformMatrix(cell.Position,
                    Matrix4x4.Translate(Vector3.up * offset) * cell.FinalMatrix);
                if (t >= 1f) cell.Landed = true;
            }
            yield return null;
        }

        routine = null;
        RestoreSourceRenderer();
        DestroyVisualLayer();
        cells.Clear();
    }

    private void RestoreSourceRenderer()
    {
        if (sourceRenderer != null)
            sourceRenderer.enabled = sourceRendererWasEnabled;
        source = null;
        sourceRenderer = null;
        sourceRendererWasEnabled = false;
    }

    private void DestroyVisualLayer()
    {
        if (visualObject != null)
        {
            visualObject.SetActive(false);   // 同帧重建时先隐去旧层，避免 Destroy 帧末前闪现上一层墙体
            Destroy(visualObject);
        }
        visualObject = null;
        visualTilemap = null;
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();

    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }
        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }
        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }

    private static float Hash01(Vector3Int position, int seed)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)position.x * 0x8DA6B343u;
            hash ^= (uint)position.y * 0xD8163841u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }
}
