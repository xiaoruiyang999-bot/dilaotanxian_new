# now_use — 当前版本实际在用脚本（v0.5.4）

> 本文件夹**只存放当前版本（场景 `v0_4_EnemySystem.unity` 回归测试 + `v0_5_Dungeon.unity` 地牢）实际运行所需的脚本**。
> 历史/弃用脚本保留在原版本文件夹（`v0_2` / `v0_3` / `v0_4` / `Framework`）作为档案，不删除。
>
> **维护约定（每个新版本必须执行）**：
> 1. 新版本中新增并在用的脚本 → 移入本文件夹（连带 .meta，GUID 不变，引用不断）。
> 2. 新版本中弃用的脚本 → 移出其所属版本文件夹归档。
> 3. 每次变动后更新本 README 的清单与版本号。
> 4. 移动用 Unity 资产移动（Editor 拖拽或 asset_move），保证 .meta 跟随，不要在外部资源管理器裸移。
>
> **v0.5.4 结构调整**：v0.5 系列收官，`v0_5/` 全部 25 个脚本迁入本文件夹 `Dungeon/`（原子目录保留），
> 原有 25 个脚本按职责归入 `Player/` `Combat/` `Enemy/` `Common/` 四个子目录；`v0_5/` 目录已删除。

## 目录结构

```
now_use/
├── Player/      玩家侧（7）
├── Combat/      武器/攻击框架，Player/Enemy 共用（7）
├── Enemy/       敌人侧 + 巡逻/血条（8）
├── Common/      通用（相机、可破坏障碍物）（3）
└── Dungeon/     地牢系统（25，保留原 v0_5 子目录）
    ├── Core/        门面/构建/配置/楼层循环（5）
    ├── Generation/  纯 C# 布局数据层（4）
    ├── Runtime/     房间运行时（4）
    ├── Content/     内容生成（7）
    └── Interaction/ walk-over 交互物（5）
```

## 清单（50 个，按目录分组）

### Player/ — 玩家侧
| 脚本 | 职责 |
|------|------|
| PlayerController | 输入接收、移动、死亡处理、Respawn（死亡重开恢复，v0.5.4），组件门面 |
| PlayerStats | HP/护甲上限、护甲脱战恢复、护甲吸收伤害 |
| Health | 玩家生命值（IDamageable），事件通知 |
| PlayerCombat | 攻击三阶段状态机，Active 驱动 WeaponHitbox |
| PlayerAimController | 鼠标瞄准方向输入层 |
| PlayerUI | 屏幕左下角固定 HP/护甲条 |
| PlayerWorldStatusBar | 头顶世界空间状态条（蓝护甲 + 红 HP） |

### Combat/ — 武器 / 攻击框架（Player/Enemy 共用）
| 脚本 | 职责 |
|------|------|
| AttackData | 攻击配置 SO（时长/范围/角度/伤害/冷却/目标层/动画），唯一数据源 |
| WeaponController | WeaponPivot 朝向、WeaponSprite 视觉、攻击方向锁定 |
| WeaponAnimator | DOTween 挥动动画（纯视觉；tween 均 SetLink，v0.5.4 修复） |
| WeaponHitbox | Active 阶段武器矩形命中检测与伤害结算 |
| AttackIndicator | 扇形/圆形预警 Mesh（纯视觉） |
| AttackQuery | 瞬时范围查询工具（无调用方，挂在 prefab 上留待技能系统） |
| IDamageable | 统一伤害接口 |

### Enemy/ — 敌人侧
| 脚本 | 职责 |
|------|------|
| EnemyController | 移动/朝向门面、受伤闪烁、死亡处理 |
| EnemyStats | 敌人属性（部分攻击字段已迁移 AttackData）+ ApplyFloorScale 楼层缩放（v0.5.4） |
| EnemyHealth | 敌人生命值（IDamageable），受击通知 AI + ScaleMaxHealth（v0.5.4） |
| EnemyCombat | 敌人攻击状态机 + 冷却 |
| EnemyAI | Patrol/Chase/Attack/ReturnToPatrol 状态机 |
| TrainingDummy | 伤害测试木桩（v0_4 场景在用） |
| PatrolSystem | 巡逻点生成（EnemyAI 使用） |
| WorldSpaceHealthBar | 敌人头顶血条 |

### Common/ — 通用
| 脚本 | 职责 |
|------|------|
| CameraFollow | 相机平滑跟随 + SnapToTarget（楼层切换/出生瞬移） |
| ObstacleHealth | 可破坏障碍物生命（IDamageable，HP=刀数，v0.5.2） |
| DestructibleObstacle | 可破坏障碍物表现（闪白/变深/销毁，v0.5.2） |

### Dungeon/Core/ — 地牢门面与楼层循环
| 脚本 | 职责 |
|------|------|
| DungeonManager | 地牢门面：Generate(seed)/Cleanup、BossRoom、Gizmos、Validate 1000 Seeds |
| DungeonBuilder | 读布局画 Tilemap、开门洞、建 Room/Door/RoomTrigger、调 Spawner 填内容 |
| DungeonConfig | 地牢配置 SO（房间数/尺寸/门宽/特殊房/楼层缩放参数） |
| RoomTypeConfig | 房间类型配置 SO（地板着色/清房条件/内容 Profile，v0.5.3） |
| RunManager | 楼层循环总控（v0.5.4）：Boss 结算、NextFloor、死亡重开、楼层 seed |

### Dungeon/Generation/ — 布局数据层（纯 C#，可离线自检）
| 脚本 | 职责 |
|------|------|
| DungeonGenerator | 网格邻接生长算法 + BFS + Boss 选址 |
| DungeonLayout | 布局数据模型（RoomType/RoomNode/RoomConnection/DungeonLayout） |
| RoomTypeAssigner | 房间类型分配（v0.5.3，叶子优先三级兜底） |
| RoomSizeExpander | Boss 2×2 / Elite 2×1 整格扩展（v0.5.3，失败回退 1×1） |

### Dungeon/Runtime/ — 房间运行时
| 脚本 | 职责 |
|------|------|
| Room | 房间状态机（Unvisited/Active/Cleared）、门管理、敌人注册与清房判定、休眠制 |
| Door | 门开/关（碰撞体 + 色块同步），仅两侧房间都非 Active 才开 |
| RoomTrigger | 玩家进房检测（四边内缩 0.5 格） |
| RoomState | 房间状态枚举 + RoomClearCondition |

### Dungeon/Content/ — 内容生成
| 脚本 | 职责 |
|------|------|
| SpawnTable | 生成权重表 SO（weight/minCount 保底/Random/Row 布局） |
| RoomContentProfile | 房间内容配置 SO（敌人/障碍/装饰/交互物四表） |
| EnemySpawner | 敌人生成（保底混编 + 洗牌 + 楼层难度注入 v0.5.4） |
| ObstacleSpawner | 障碍物生成（可破坏款挂 ObstacleHealth） |
| DecorationSpawner | 装饰生成（旋转/缩放/颜色抖动） |
| InteractableSpawner | 交互物生成（散点冲突重试 / Row 一列陈列） |
| SpawnPositionHelper | 生成位置合法性（距墙/距门/防重叠/重试上限） |

### Dungeon/Interaction/ — walk-over 交互物
| 脚本 | 职责 |
|------|------|
| Interactable | 交互基类：一次性触发 + OnConsumed 钩子（v0.5.4）+ 压暗已消耗态 |
| ChestInteractable | 宝箱：三段式开箱动画（盖片分开 → 道具 pop-in → 结算，v0.5.4） |
| ShrineInteractable | 事件祭坛：随机 ±（治疗/受伤，运行时事件不进种子流） |
| SupplyInteractable | 商店补给：治疗球/护甲球（免费占位） |
| PortalInteractable | 传送门（v0.5.4）：石块漩涡动效，踩门 → RunManager.NextFloor |

## 明确不在用（留在档案目录）
- `v0_2/CharacterInput.cs`、`v0_2/WarriorCharacter.cs` — v0.2 旧场景专用
- `v0_3/Enemy.cs`、`v0_3/WarriorAttack.cs`、`v0_3/Interfaces/IAttack.cs` — 已弃用
- `Framework/DamageSystem.cs`、`Framework/DetectionSystem.cs` — 未被任何脚本引用
