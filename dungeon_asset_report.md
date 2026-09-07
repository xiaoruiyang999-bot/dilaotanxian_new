# Dungeon 素材与生成管线调研报告

范围：`Assets/Scripts/now_use/Dungeon/`（Tiles / Generation / Core / Content / Runtime）。
路径存在性均已用 glob/文件系统核验；4 个 Resources 目录共 114 张 PNG 的 .meta 全部 `spriteMode: 1`（Single）。

## 1. 生成管线调用链

- `DungeonManager.Start→Generate`：`Layout = DungeonGenerator.Generate(config, seed)`（Core/DungeonManager.cs:80）
- `DungeonManager.Generate`：`builder.Build(Layout, config, seed, floorNumber)`（Core/DungeonManager.cs:81）
- `DungeonGenerator.Generate`：邻接生长 `TryGrow`（Generation/DungeonGenerator.cs:41）→ 类型 `RoomTypeAssigner.Assign`（:28）→ 扩格 `RoomSizeExpander.Expand`（:29）
- `DungeonBuilder.Build`：`ClearAll` → `GroundTileset.Load`（Core/DungeonBuilder.cs:78,83）→ `TerrainMask.Generate`（:93）→ 预算门洞 `ComputeDoorRect`（:103）→ 逐房 `PaintRoom`（:121）
- `PaintRoom`：外墙线 `SetWallTile`（Core/DungeonBuilder.cs:254-259）；`RoomPlanner.CreatePlan` 产出 RoomPlan（:269）；写回 `skeletonCells/roomSkeletons/roomSpawnCells`（:271-273）
- `RoomPlan.Plain/RoomPlanner.CreatePlan`：房形 RoomShapeGenerator → 轮廓 RoomBoundaryBuilder → 障碍 RoomObstaclePlanner → 验证 RoomLayoutValidator → `RefreshSpawnCells` 内容白名单（Generation/RoomPlan.cs:71-100）
- `Build`：开洞 `OpenDoor→OpenDoorTile`（Core/DungeonBuilder.cs:122,401,421）→ 骨架偏置 `ApplySkeletonBias`（:127）→ 墙定向 `ReorientWalls`（:131,354）→ 物理刷新 `FlushWallColliderChanges`（:132,341）
- `Build`：墙坠落 `WallDropAnimator.Play`（Core/DungeonBuilder.cs:143）→ 地皮 `BuildRoadMaskSurface→TerrainPainter.PaintCell`（:147,431,441）
- `Build`：建房/门/内容 `CreateRoomObject`（:148,446）、`CreateDoor`（:149,485）、`SpawnContent→Enemy/Decoration/Stone/Barrel/Interactable Spawner`（:152,159-189）
- 楼层循环：`RunManager.NextFloor → dungeonManager.Cleanup→Generate(FloorSeed())`（Core/RunManager.cs:144-147）

## 2. 素材清单

| 资产路径 | 加载方式 | 使用文件:行号 | 用途 | 存在 |
|---|---|---|---|---|
| `Assets/Resources/Art/Tiles/Ground/*.png`（grass_base, grass_clean_1~8, dirt_base, dirt_core_01~08, dirt_interior_1~4, edge×12, corner_tl/bl, multi_v，共 36 张 85px） | Resources.LoadAll\<Sprite\>（Tiles/GroundTileset.cs:14,27）→运行时 ScriptableObject Tile 池缓存（:80） | 地面绘制 TerrainPainter.cs:28-69（经 DungeonBuilder.cs:441） | 地面基底/土斑装饰/草土过渡边角 | ✓ |
| `Assets/Resources/Art/Tiles/Ground/grass_base.png`、`dirt_base.png`（作为 Texture2D） | Resources.Load\<Texture2D\>（Tiles/RoadMaskGroundSurface.cs:12-13,21-22）+ `Shader.Find("Dungeon/RoadMaskGround")`（:23） | 休眠代码（Build() 在 Dungeon 内无调用者） | 整层 Mesh+Shader 地皮底图（已停用） | ✓（shader：Assets/Shaders/RoadMaskGround.shader） |
| `Assets/Resources/Art/Decor/WallProps/prop_01,03,09,16,18,19,26,27.png`（8 张） | Resources.LoadAll\<Sprite\>（Tiles/WallPropTileset.cs:18,44）+ 运行时 Sprite.Create 归一化（:52-58） | 墙体铺设 DungeonBuilder.cs:330-335,354-371 | 墙（prop_01 竖列堆叠，其余横行随机） | ✓ |
| `Assets/Resources/Art/Tiles/Wall/wall_00~15.png` | Resources.Load\<Sprite\>（Tiles/WallAutotiler.cs:16,56） | 16 掩码 Autotile——**类无任何调用者**（休眠） | 墙（16 图拼接，待补齐） | ✗（目录不存在，代码静默回退） |
| `Assets/Resources/Art/Decor/Stones/prop_01~34.png`（34 张） | Resources.LoadAll\<Sprite\>（Content/StoneDecorSpawner.cs:17,91） | 实例化 SpriteRenderer（:60-81） | 废墟石块装饰（小）/障碍物（≥1.2u） | ✓ |
| `Assets/Resources/Art/Decor/Barrels/barrel_00~28,37~43.png`（34 张，文件 bucket.cs） | Resources.LoadAll\<Sprite\>（Content/bucket.cs:14,98） | 实例化 SpriteRenderer（:55-88） | 木桶装饰（Trigger 可打碎）/大型木箱堆（实体障碍） | ✓ |
| 场景序列化引用：`floorTile`/`wallTile`（TileBase）、`floorTilemap`/`wallsTilemap`、`doorPrefab`、`roomTypeConfigs` | 场景/Inspector 序列化（Core/DungeonBuilder.cs:13-28） | 地板（无地皮时回退，:294）/白方块墙回退（:334,369）/门 Prefab 实例化（:494）/类型配置 | 地面回退、墙回退、门、配置 | ✓（脚本侧引用存在） |
| `SpawnTable` 条目 prefab（敌/装饰/交互物/商店 Row） | SpawnTable(SO) 数据引用 + Object.Instantiate（Content/SpawnTable.cs:14；EnemySpawner.cs:57、DecorationSpawner.cs:26、InteractableSpawner.cs:42,71、ObstacleSpawner.cs:28） | SpawnContent 驱动（Core/DungeonBuilder.cs:166-189） | 敌人/装饰/宝箱/祭坛/补给 | 脚本侧 ✓（具体 prefab 由表驱动，不在本目录） |
| `rewardChestPrefab`/`portalPrefab` | 场景序列化 + Instantiate（Core/RunManager.cs:17-18,130,133） | Boss 清房奖励（Core/RunManager.cs:129-135） | 奖励宝箱/传送门 | ✓（脚本侧引用存在） |

## 3. 三类呈现体系

- **Tilemap + TileBase（批量渲染，唯一地面/墙体系）**：Floor Tilemap（GroundTileset 地面池，Tile.transform 90° 旋转补全方向）；Walls Tilemap（WallPropTileset 归一化 Tile + ColliderType.Grid 整格碰撞）；`floorTile/wallTile` 序列化回退瓦片；WallAutotiler 16 掩码池（休眠）；WallDropAnimator 临时视觉 Tilemap（无碰撞，Tiles/WallDropAnimator.cs:65-101）。
- **SpriteRenderer 实例（不池化，每层 Destroy 随 dungeonRoot 重建）**：EnemySpawner 敌人、DecorationSpawner 装饰（缩放/颜色抖动）、InteractableSpawner 交互物、ObstacleSpawner（休眠）、StoneDecorSpawner 石块（sr.sortingOrder=3 + BoxCollider2D + ObstacleHealth）、bucket.cs 木桶（小 Trigger/大实体）、Door Prefab 的 `[SerializeField] SpriteRenderer visual`（Runtime/Door.cs:10,45）、RunManager 宝箱/传送门。
- **运行时程序生成（非 Sprite 资产）**：WallPropTileset.Sprite.Create 归一化精灵（`_wall_norm` 命名，Tiles/WallPropTileset.cs:52-58）；RoadMaskGroundSurface 运行时 Texture2D/Mesh/Material（`RoadMask_*` 命名，Tiles/RoadMaskGroundSurface.cs:68-77,132）——休眠。

## 4. 红线对照

- **R3（图片 spriteMode=Single）**：合规。Ground/WallProps/Barrels/Stones 全部 114 个 .meta 均为 `spriteMode: 1`（已 grep 核验）。
- **R21（Sprite.Create 立即命名）**：合规。WallPropTileset.cs:52-58 创建后同帧 `normalized.name = s.name + "_wall_norm"`（v1.1.40 修复注释佐证历史 bug）。
- **R9（物理查询零 GC / Tilemap 批量写）**：合规为主。SpawnPositionHelper 用静态 `Collider2D[]` 缓冲 + ContactFilter2D 复用（Content/SpawnPositionHelper.cs:22,34,57）；墙体变更统一 `ProcessTilemapChanges()+Physics2D.SyncTransforms()` 一次提交（Core/DungeonBuilder.cs:341-347）。风险点：WallDropAnimator 动画期间每帧对临时层逐格 `SetTransformMatrix`（Tiles/WallDropAnimator.cs:119），数百格×~0.6s 持续重建该层，属表现期峰值开销。
- **红线6（`#if UNITY_EDITOR` 便利代码打包风险）**：合规。DungeonManager.cs:220-228、DungeonBuilder.cs:508-522、RunManager.cs:175-215 的 MenuItem/FindAnyObjectByType 调试代码均在 #if 块内，不参与构建（仅编辑器内调试，无 AssetDatabase 调用）。

## 5. 素材使用上的坑与改进点（≤5 条）

1. `Art/Tiles/Wall/wall_00~15.png` 不存在且 WallAutotiler 无任何调用者——整份休眠代码守着一份永不满足的 16 图契约；要么补图接线，要么删除（Tiles/WallAutotiler.cs:56,60）。
2. RoadMaskGroundSurface 已休眠但代码保留，`Shader.Find` 靠字符串查找（构建 strip 后找不到只能运行期 LogError），复活前先确认 shader include 与 shader 资产稳定（Tiles/RoadMaskGroundSurface.cs:23-27）。
3. WallPropTileset 每次 Load 都 Sprite.Create 新归一化精灵且静态缓存永不清空——跨楼层/重进场景旧 Tile(SO) 与纹理引用无法 GC（轻微泄漏）；楼层重建时应提供 Clear 钩子（Tiles/WallPropTileset.cs:21-23,41-75）。
4. `wallTile`/`floorTile` 序列化字段实为“素材缺失回退白块”，Inspector 语义与实际主素材路径（Resources）分离，极易误导维护者去场景改墙皮（Core/DungeonBuilder.cs:17-18,334）。
5. 文件 `bucket.cs` 内类名为 `BarrelDecorSpawner`、素材为 `barrel_NN`，三套命名不一致，检索/引用易踩坑（Content/bucket.cs:12,14）。
