using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>用正式地牢场景的序列化配置验证单节点绘制，不保存或改动场景资产。</summary>
public class V211SingleRoomBuildTests
{
    [Test]
    public void OfficialScene_BuildsOnlyCurrentNodeAcrossTransitions()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/v0_7_ClassWeapon.unity");
        try
        {
            DungeonManager manager = FindInScene<DungeonManager>(scene);
            DungeonBuilder builder = FindInScene<DungeonBuilder>(scene);
            Assert.NotNull(manager);
            Assert.NotNull(builder);
            var field = typeof(DungeonManager).GetField("config",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var config = field.GetValue(manager) as DungeonConfig;
            Assert.NotNull(config);

            NodeType[] types = { NodeType.Start, NodeType.Supply, NodeType.Sage,
                NodeType.Shop, NodeType.Boss };
            for (int i = 0; i < types.Length; i++)
            {
                var node = new DungeonGraphNode
                {
                    NodeId = 100 + i, Type = types[i], Column = i,
                    RoomSeed = 1223 + i, EncounterSeed = 3456 + i, ShopSeed = 6789 + i,
                };
                builder.BuildSingle(node, config, 1, objectiveCompleted: false,
                    out Vector3 spawn);
                Assert.AreEqual(1, builder.Rooms.Count, $"{types[i]} 必须仅保留一间房");
                Assert.IsTrue(builder.Rooms.TryGetValue(node.NodeId, out Room room));
                Assert.Greater(spawn.x, room.Bounds.xMin);
                Assert.Less(spawn.x, room.Bounds.center.x);
                Assert.IsTrue(CameraFollow.HasMapBounds);
                Assert.AreEqual(room.Bounds.xMin - 1f, CameraFollow.MapBounds.xMin);
                Assert.AreEqual(room.Bounds.yMin - 1f, CameraFollow.MapBounds.yMin);
                Assert.AreEqual(room.Bounds.xMax + 1f, CameraFollow.MapBounds.xMax);
                Assert.AreEqual(room.Bounds.yMax + 1f, CameraFollow.MapBounds.yMax);
                if (types[i] == NodeType.Shop)
                    Assert.AreEqual(config.linearRoomHeight, room.Bounds.height,
                        "新 DAG 商店房需要完整高度，才能容纳当前相机视野");
            }
        }
        finally
        {
            CameraFollow.ClearMapBounds();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
