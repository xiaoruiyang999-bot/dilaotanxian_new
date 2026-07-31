# now_use — 当前版本实际在用脚本（v0.7.0）

> 本文件夹**只存放当前版本（场景 `v0_4_EnemySystem.unity` 回归测试 + `v0_5_Dungeon.unity` / `v0_6_ClassWeapon.unity` 地牢）实际运行所需的脚本**。
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
├── Player/      玩家侧（10）
├── Combat/      武器/攻击框架 + 子弹系统 + 伤害管线（v0.7.0），Player/Enemy 共用（11）
├── Enemy/       敌人侧 + 巡逻/血条（8）
├── Common/      通用（相机、可破坏障碍物、TMP 字体）（4）
├── Class/       职业系统 + 准备房间（9，v0.6.2；六维字段 v0.7.0）
├── Weapon/      武器框架 + 运行时视觉（7，v0.6.3 行为完整实现）
└── Dungeon/     地牢系统（29，保留原 v0_5 子目录）
    ├── Core/        门面/构建/配置/楼层循环（5）
    ├── Generation/  纯 C# 布局数据层（4）
    ├── Runtime/     房间运行时（4）
    ├── Content/     内容生成（7）
    └── Interaction/ E 键交互物 + 拾取框架 + 法力掉落（9）
```

## 清单（78 个，按目录分组）

### Player/ — 玩家侧
| 脚本 | 职责 |
|------|------|
| PlayerController | 输入接收（Move/Attack/Interact/Cancel/Skill 分发，Skill 为 v0.6.4 占位；Dash/Sprint 分发 v0.7.0 下线，action 保留在 .inputactions 备用）、死亡处理、Respawn，组件门面；移动写入已迁移 PlayerMovement；v0.6.3 Attack 改 started/canceled 转发（按下/松开，支持蓄力与连发） |
| PlayerMovement | 移动执行层（v0.7.0 重写为纯移动）：常规移动 + 蓄力减速 ×0.5（SetChargeSlow），FixedUpdate 统一写速度；闪避/奔跑/体力已下线 |
| PlayerInteractor | E 键交互 + 两段式拾取（v0.6.1）：候选探测/呼吸高亮/"按 E"标签/纯文字拾取列表 |
| PlayerStats | HP/护甲/法力上限（法力 v0.6.2 不可自动回复）+ 六维（v0.7.0：攻击/暴击率/暴伤/护甲双倍率 R·L，R/L 结算 v0.7.1 接线）、护甲脱战恢复（v0.7.1 删）、护甲吸收伤害、ApplyClass(ClassData)；体力系统 v0.7.0 下线 |
| Health | 玩家生命值（IDamageable），事件通知；无敌标记 SetInvincible/IsInvincible（v0.6.0） |
| PlayerCombat | 近战三阶段状态机 + v0.6.3 三模式（Melee/Ranged/SelfCast）：蓄力状态机（移动×0.5、AttackData 运行时副本缩放范围/角度）、弹夹/换弹/闲置自动换弹计时、Projectile 开火、治疗自施法；弹药/换弹/武器展示事件供 AmmoUI 订阅；v0.7.0 满蓄倍率归位 WeaponHitbox.DamageMultiplier（不再 SetDamage 改副本） |
| PlayerAimController | 鼠标瞄准方向输入层 |
| PlayerUI | 屏幕左下角固定 HP/护甲/法力条（法力条 v0.6.2）；v0.7.0 体力条下线，场景残留 StaminaBar 对象运行时 SetActive(false) 兜底隐藏 |
| PlayerWorldStatusBar | 头顶世界空间状态条（钢灰护甲 + 红 HP；黄体力条 v0.7.0 下线） |
| AmmoUI | 弹药面板（v0.6.3）：武器色块+名 + 弹夹 x/y + 换弹进度细条（运行时构建，订阅 PlayerCombat 事件；无弹夹/默认近战隐藏） |

### Combat/ — 武器 / 攻击框架（Player/Enemy 共用）
| 脚本 | 职责 |
|------|------|
| AttackData | 攻击配置 SO（时长/范围/角度/伤害/冷却/目标层/动画），唯一数据源；v0.6.3 +CreateRuntimeCopy/运行时副本 setter（近战蓄力缩放只作用于副本） |
| WeaponController | WeaponPivot 朝向、WeaponSprite 视觉、攻击方向锁定；v0.6.3 +SetAttackData/+宽度倍率（枪矛蓄力）/+自定义视觉挂载（模块化手持视觉替换默认色块） |
| WeaponAnimator | DOTween 挥动动画（纯视觉；tween 均 SetLink，v0.5.4 修复） |
| WeaponHitbox | Active 阶段武器矩形命中检测与伤害结算；v0.6.3 宽度改实时读 WeaponController + LengthMultiplier 长度倍率（戳击伸展用）；v0.7.0 +DamageMultiplier 伤害倍率（蓄力归位，BeginSwing 复位）+ 玩家/敌人结算分流（根上 PlayerStats 判定：玩家走 DamageResolver 新管线，敌人直扣原路径） |
| AttackIndicator | 扇形/圆形/矩形（Box，v0.6.3）预警 Mesh（纯视觉）；detachOnShow 控制脱离父物体（敌人=true 原地预警，玩家=false 跟随） |
| AttackQuery | 瞬时范围查询工具（无调用方，挂在 prefab 上留待技能系统） |
| IDamageable | 统一伤害接口 |
| DamageContext | 伤害上下文 struct（v0.7.0）：baseAttack（角色攻击+武器攻击）/multiplier（倍率区）/critRate/critDamage；Roll() 一次暴击判定返回最终伤害，IsCrit 外露供表现层 |
| DamageResolver | 伤害结算静态入口（v0.7.0）：Deal(target, ctx) 单点收口；v0.7.1 在此分流 Health 减伤甲结算 |
| ProjectileData | 子弹配置 SO（v0.6.3）：速度/伤害/存活/半径/视觉类型/配色/目标层；资产在 Assets/Data/ |
| Projectile | 子弹（v0.6.3）：直线飞行 + Trigger 命中 IDamageable + 撞墙（Default 层）销毁 + 存活兜底 + 通用命中特效；v0.7.0 玩家子弹走 DamageResolver（owner 根查 PlayerStats，damageMul 映射 ctx.multiplier），敌人子弹原路径 |

### Enemy/ — 敌人侧
| 脚本 | 职责 |
|------|------|
| EnemyController | 移动/朝向门面、受伤闪烁、死亡处理；v0.6.3 死亡按 EnemyStats.manaOrbValue 掉法力球 |
| EnemyStats | 敌人属性（部分攻击字段已迁移 AttackData）+ ApplyFloorScale 楼层缩放（v0.5.4）+ manaOrbValue 击杀掉蓝（v0.6.3：普通 3/精英 8/Boss 20） |
| EnemyHealth | 敌人生命值（IDamageable），受击通知 AI + ScaleMaxHealth（v0.5.4） |
| EnemyCombat | 敌人攻击状态机 + 冷却；攻击触发判定 = AttackData.AttackRange + 0.3 缓冲（v0.6.0） |
| EnemyAI | Patrol/Chase/Attack/ReturnToPatrol 状态机；Update 决策 + FixedUpdate 统一写速度（v0.6.0 抖动修复），无用的 attackData 字段已移除 |
| TrainingDummy | 伤害测试木桩（v0_4 场景在用） |
| PatrolSystem | 巡逻点生成（EnemyAI 使用） |
| WorldSpaceHealthBar | 敌人头顶血条 |

### Common/ — 通用
| 脚本 | 职责 |
|------|------|
| CameraFollow | 相机平滑跟随 + SnapToTarget（楼层切换/出生瞬移） |
| ObstacleHealth | 可破坏障碍物生命（IDamageable，HP=刀数，v0.5.2） |
| DestructibleObstacle | 可破坏障碍物表现（闪白/变深/销毁，v0.5.2） |
| TMPFontProvider | 全局 TMP 字体（v0.6.2：运行时微软雅黑动态 TMP_FontAsset，全局缓存） |

### Class/ — 职业系统 + 准备房间（v0.6.2）
| 脚本 | 职责 |
|------|------|
| ClassType | 职业枚举（Warrior/Archer/Mage） |
| ClassData | 职业配置 SO：三属性上限 + 六维字段（v0.7.0：攻击/暴击率/暴伤/护甲双倍率 R·L，占位默认值同 PlayerStats）/职业色/可用武器列表；资产在 Assets/Data/Class/ |
| ClassCatalog | 职业资产目录（编辑器 AssetDatabase 加载，构建需 Resources/Class/） |
| PrepPedestal | 准备房间展台（职业选择台/武器展示台，运行时多色块视觉，E 交互；名签参数序列化可调） |
| PrepRoomPlacer | 三展台布置 + 武器展台刷新 + 初始武器自动归位（仅供准备场景，阶段 C 重构签名） |
| ClassSelectUI | 职业选择界面（TMP 屏幕空间）：选择→高亮→确认闪烁→ApplyClass→展台刷新；v0.7.0 职业按钮描述改六维数值行（HP/护甲/攻击/魔力/暴击%/暴伤×） |
| RunStateCarrier | 跨场景配置载体（DontDestroyOnLoad）：LastChosenClass/LastWeapon/HasLoadout |
| PrepRoomManager | 独立准备场景总控：房间视觉/展台/传送门/出生位/换武器归位订阅 |
| PrepPortalInteractable | 准备场景进入地牢传送门：校验 HasLoadout → LoadScene |

### Weapon/ — 武器框架（v0.6.2 框架 / v0.6.3 完整实现）
| 脚本 | 职责 |
|------|------|
| WeaponData | 武器配置 SO（职业/行为类型/攻击引用/子弹引用/自疗量/蓄力规则与参数/弹夹射速/染色/图标）；资产在 Assets/Data/Weapon/ |
| WeaponInstance | 武器运行时状态（弹夹/换弹/蓄力计时），纯 C# 类 |
| WeaponBehavior | 行为基类 + 三派生分发（v0.6.3）：Melee → PlayerCombat 近战链；Ranged → Projectile 开火；SelfCast → 治疗自施法 |
| PlayerWeaponHolder | 玩家武器持有与装备入口：换武器旧武器原地掉落（dropOldWeaponOnEquip 可关）；OnWeaponChanged 事件（准备场景归位订阅） |
| WeaponPickup | 武器拾取物（IPickupable）：职业校验"职业不符"拒绝，符合则 Equip；v0.6.3 地图掉落视觉 = WeaponVisualBuilder 小图标 + 职业色底板 |
| WeaponVisualBuilder | 运行时武器视觉（v0.6.3）：六武器模块化多色块手持视觉（含 Effect 蓄力发光部件）+ 地图掉落小图标 |
| ProjectileVisualBuilder | 运行时子弹视觉（v0.6.3）：箭矢/弩矢/能量弹/精灵弹四种子弹拼接 + 通用命中特效 + 共享白图/圆图 Sprite 缓存 |

### Dungeon/Core/ — 地牢门面与楼层循环
| 脚本 | 职责 |
|------|------|
| DungeonManager | 地牢门面：Generate(seed)/Cleanup、BossRoom、Gizmos、Validate 1000 Seeds |
| DungeonBuilder | 读布局画 Tilemap、开门洞、建 Room/Door/RoomTrigger、调 Spawner 填内容 |
| DungeonConfig | 地牢配置 SO（房间数/尺寸/门宽/特殊房/楼层缩放参数） |
| RoomTypeConfig | 房间类型配置 SO（地板着色/清房条件/内容 Profile，v0.5.3） |
| RunManager | 楼层循环总控（v0.5.4）：Boss 结算、NextFloor；阶段 C（R4）：Start 应用 RunStateCarrier 职业/武器，死亡 → 加载准备场景 |

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

### Dungeon/Interaction/ — E 键交互物 + 拾取框架
| 脚本 | 职责 |
|------|------|
| Interactable | 交互基类（v0.6.1：walk-over → E 键 Interact()）：一次性触发 + OnConsumed 钩子 + 压暗已消耗态 |
| IPickupable | 可拾取物接口（v0.6.1 两段式拾取框架）：DisplayName + OnPickedUp |
| HealPickup | 治疗球拾取物（v0.6.1）：宝箱落物，按 E 拾取 +2HP，运行时补触发器 |
| ChestInteractable | 宝箱：三段式开箱动画；v0.6.3 奖励 = 本职业随机武器 / 法力瓶 50-50（manaBottleChance 可调，无职业兜底 HealPickup） |
| ShrineInteractable | 事件祭坛：随机 ±（治疗/受伤，运行时事件不进种子流） |
| SupplyInteractable | 商店补给：治疗球/护甲球/法力瓶（v0.6.3 +Mana 分支，免费占位） |
| PortalInteractable | 传送门（v0.5.4）：石块漩涡动效，按 E → RunManager.NextFloor |
| ManaOrb | 击杀掉落小法力球（v0.6.3）：walk-over 自动吸附（不占 E、不进拾取列表），AddMana 后飞入销毁 |
| ManaBottlePickup | 法力瓶（v0.6.3，IPickupable）：E 拾取 +40 法力；宝箱掉落 + 商店陈列（Supply_ManaBottle prefab） |

## 明确不在用（留在档案目录）
- `v0_2/CharacterInput.cs`、`v0_2/WarriorCharacter.cs` — v0.2 旧场景专用
- `v0_3/Enemy.cs`、`v0_3/WarriorAttack.cs`、`v0_3/Interfaces/IAttack.cs` — 已弃用
- `Framework/DamageSystem.cs`、`Framework/DetectionSystem.cs` — 未被任何脚本引用
