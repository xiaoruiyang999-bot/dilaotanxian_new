using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

public static class V0_1_InitialHouseBuilder
{
    [MenuItem("Dilaotanxian/v0.1 Build Initial House")]
    public static void Build()
    {
        // 1. Ensure folder structure
        EnsureFolder("Assets/Sprites");
        EnsureFolder("Assets/Sprites/v0_1");
        EnsureFolder("Assets/Tilemaps");
        EnsureFolder("Assets/Tilemaps/v0_1");
        EnsureFolder("Assets/Palettes");
        EnsureFolder("Assets/Palettes/v0_1");
        EnsureFolder("Assets/Scenes");

        // 2. Create plain white Sprite assets (64x64 so 1 unit per tile at PPU=64)
        Sprite whiteSquare = CreateWhiteSprite("Assets/Sprites/v0_1/WhiteSquare.asset");
        Sprite whiteSquareWall = CreateWhiteSprite("Assets/Sprites/v0_1/WhiteSquare_Wall.asset");

        // 3. Create colored Tiles
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        floorTile.name = "FloorTile";
        floorTile.sprite = whiteSquare;
        floorTile.color = new Color(0.85f, 0.75f, 0.65f, 1f);
        floorTile.colliderType = Tile.ColliderType.None;
        AssetDatabase.CreateAsset(floorTile, "Assets/Tilemaps/v0_1/FloorTile.asset");

        Tile wallTile = ScriptableObject.CreateInstance<Tile>();
        wallTile.name = "WallTile";
        wallTile.sprite = whiteSquareWall;
        wallTile.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        wallTile.colliderType = Tile.ColliderType.Sprite;
        AssetDatabase.CreateAsset(wallTile, "Assets/Tilemaps/v0_1/WallTile.asset");

        // 4. Create a minimal Tile Palette prefab containing both tiles
        CreatePalette("Assets/Palettes/v0_1/InitialHouse.prefab", floorTile, wallTile);

        // 5. Create the initial house scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            camera = camGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
        }
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        camera.transform.position = new Vector3(4.5f, 3.5f, -10f);

        // Global 2D light for URP
        GameObject lightGo = new GameObject("Global Light 2D");
        Light2D globalLight = lightGo.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.intensity = 1f;
        globalLight.color = Color.white;

        // Grid
        GameObject gridGo = new GameObject("Grid");
        Grid grid = gridGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        // Floor Tilemap
        GameObject floorGo = new GameObject("Floor");
        floorGo.transform.SetParent(gridGo.transform);
        floorGo.transform.localPosition = Vector3.zero;
        Tilemap floorTm = floorGo.AddComponent<Tilemap>();
        TilemapRenderer floorRend = floorGo.AddComponent<TilemapRenderer>();
        floorRend.sortingOrder = 0;

        // Walls Tilemap
        GameObject wallsGo = new GameObject("Walls");
        wallsGo.transform.SetParent(gridGo.transform);
        wallsGo.transform.localPosition = Vector3.zero;
        Tilemap wallsTm = wallsGo.AddComponent<Tilemap>();
        TilemapRenderer wallsRend = wallsGo.AddComponent<TilemapRenderer>();
        wallsRend.sortingOrder = 1;

        // Draw 10x8 floor (interior 8x6 walkable)
        for (int x = 1; x <= 8; x++)
            for (int y = 1; y <= 6; y++)
                floorTm.SetTile(new Vector3Int(x, y, 0), floorTile);

        // Draw enclosing walls
        for (int x = 0; x <= 9; x++)
        {
            wallsTm.SetTile(new Vector3Int(x, 0, 0), wallTile);
            wallsTm.SetTile(new Vector3Int(x, 7, 0), wallTile);
        }
        for (int y = 1; y <= 6; y++)
        {
            wallsTm.SetTile(new Vector3Int(0, y, 0), wallTile);
            wallsTm.SetTile(new Vector3Int(9, y, 0), wallTile);
        }

        // Wall collision
        TilemapCollider2D tmc = wallsGo.AddComponent<TilemapCollider2D>();
        CompositeCollider2D cc = wallsGo.AddComponent<CompositeCollider2D>();
        Rigidbody2D rb = wallsGo.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        tmc.usedByComposite = true;

        // Test player (green circle)
        GameObject playerGo = new GameObject("TestPlayer");
        SpriteRenderer sr = playerGo.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite("Assets/Sprites/v0_1/WhiteCircle.asset");
        sr.color = new Color(0.2f, 0.8f, 0.3f, 1f);
        sr.sortingOrder = 2;
        playerGo.transform.position = new Vector3(4.5f, 3.5f, 0f);
        playerGo.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        Rigidbody2D prb = playerGo.AddComponent<Rigidbody2D>();
        prb.gravityScale = 0f;
        prb.constraints = RigidbodyConstraints2D.FreezeRotation;

        playerGo.AddComponent<CircleCollider2D>();

        // Save scene and assets
        string scenePath = "Assets/Scenes/v0_1_InitialHouse.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();

        Debug.Log("[v0.1] Initial house scene built at: " + scenePath);
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Sprite CreateWhiteSprite(string assetPath)
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(sprite, assetPath);
        AssetDatabase.AddObjectToAsset(tex, sprite);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return sprite;
    }

    private static Sprite CreateCircleSprite(string assetPath)
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f - 1f;
        float radiusSq = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                tex.SetPixel(x, y, dx * dx + dy * dy <= radiusSq ? white : clear);
            }
        }
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(sprite, assetPath);
        AssetDatabase.AddObjectToAsset(tex, sprite);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return sprite;
    }

    private static void CreatePalette(string prefabPath, Tile floorTile, Tile wallTile)
    {
        GameObject paletteRoot = new GameObject("InitialHouse");
        Grid grid = paletteRoot.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        Tilemap tilemap = paletteRoot.AddComponent<Tilemap>();
        TilemapRenderer renderer = paletteRoot.AddComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Chunk;

        tilemap.SetTile(new Vector3Int(0, 0, 0), floorTile);
        tilemap.SetTile(new Vector3Int(1, 0, 0), wallTile);

        PrefabUtility.SaveAsPrefabAsset(paletteRoot, prefabPath);
        Object.DestroyImmediate(paletteRoot);
    }
}
