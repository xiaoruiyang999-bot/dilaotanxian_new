using UnityEditor;
using UnityEngine;

public sealed class GroundTileImportContract : AssetPostprocessor
{
    private const string GroundFolder = "Assets/Resources/Art/Tiles/Ground/";
    public const int TilePixels = 85;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(GroundFolder, System.StringComparison.OrdinalIgnoreCase)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = TilePixels;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = false;
        importer.isReadable = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 1;
        importer.SetTextureSettings(settings);
    }
}