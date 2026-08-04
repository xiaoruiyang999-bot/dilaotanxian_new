# now_use — 当前版本实际在用脚本（v0.7.4）

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
├── Player/      玩家侧（11）
├── Combat/      武器/攻击框架 + 子弹系统 + 伤害管线（v0.7.0），Player/Enemy 共用（11）
├── Enemy/       敌人侧 + 巡逻/血条（8）
├── Common/      通用（相机、可破坏障碍物、TMP 字体）（4）
├── Class/       职业系统 + 准备房间（9，v0.6.2；六维字段 v0.7.0）
├── Weapon/      武器框架 + 运行时视觉（7，v0.6.3 行为完整实现）
├── Item/        道具/背包（4，v0.7.3 三种正式消耗品：血包/甲包/魔力恢复包）
├── Skill/       技能框架（5，v0.7.4：SkillData/分支表/资产目录/执行器）
└── Dungeon/     地牢系统（29，保留原 v0_5 子目录）
    ├── Core/        门面/构建/配置/楼层循环（5）
    ├── Generation/  纯 C# 布局数据层（4）
    ├── Runtime/     房间运行时（4）
    ├── Content/     内容生成（7）
    └── Interaction/ E 键交互物 + 拾取框架 + 法力掉落（9）
```

## 清单（88 个，按目录分组）

### Player/ — 玩家侧
| 脚本 | 职责 |
|------|------|
| PlayerController | 输入接收（Move/Attack/Interact/Cancel/Skill/Ultimate/WeaponSkill/UseItem 分发；v0.7.4 Skill/Ultimate/WeaponSkill → SkillExecutor.TryCastSlot(0/1/2)，F=小技能 Q=大招 R=武器技能；UseItem v0.7.2 → ItemInventory.UseActive；Dash/Sprint 分发 v0.7.0 下线，action 保留在 .inputactions 备用）、死亡处理、Respawn，组件门面；移动写入已迁移 PlayerMovement；v0.6.3 Attack 改 started/canceled 转发（按下/松开，支持蓄力与连发）；v0.7.2 Awake 运行时挂载 ItemInventory，v0.7.4 同模式挂载 SkillExecutor |
| PlayerMovement | 移动执行层（v0.7.0 重写为纯移动）：常规移动 + 蓄力减速 ×0.5（SetChargeSlow），FixedUpdate 统一写速度；闪避/奔跑/体力已下线 |
| PlayerInteractor | E 键交互 + 实时拾取列表（v0.7.2 改版）：普通交互物最近候选（呼吸高亮+"按 E"标签）；可拾取物实时列表——靠近自动进/走远自动出、选中项场景呼吸放大、滚轮/数字键切换、E 拾取选中项 |
| PlayerStats | HP/护甲/法力上限（法力 v0.6.2 不可自动回复）+ 六维（v0.7.0：攻击/暴击率/暴伤/护甲双倍率 R·L）、减伤甲结算 ApplyArmorDamage（v0.7.1：调 DamageResolver.ApplyArmor，R/L 由 OnValidate 钳制）、ModifyArmor（甲包/护甲球）、ApplyClass(ClassData)；体力系统 v0.7.0 下线、呼吸回甲 v0.7.1 删除 |
| Health | 玩家生命值（IDamageable），事件通知；无敌标记 SetInvincible/IsInvincible（v0.6.0）；TakeDamage 经 PlayerStats.ApplyArmorDamage 走减伤甲结算（v0.7.1） |
| PlayerCombat | 近战三阶段状态机 + v0.6.3 三模式（Melee/Ranged/SelfCast）：蓄力状态机（移动×0.5、AttackData 运行时副本缩放范围/角度）、弹夹/换弹/闲置自动换弹计时、Projectile 开火、治疗自施法；弹药/换弹/武器展示事件供 AmmoUI 订阅；v0.7.0 满蓄倍率归位 WeaponHitbox.DamageMultiplier（不再 SetDamage 改副本） |
| PlayerAimController | 鼠标瞄准方向输入层 |
| PlayerUI | 屏幕左下角固定 HP/护甲/法力条（法力条 v0.6.2）；v0.7.0 体力条下线，场景残留 StaminaBar 对象运行时 SetActive(false) 兜底隐藏 |
| PlayerWorldStatusBar | ~~头顶世界空间状态条~~（钢灰护甲 + 红 HP；黄体力条 v0.7.0 下线）。**已从 Player.prefab 移除组件（2026-08-03）**：屏幕状态条美术到位后头顶条冗余；类文件保留，想恢复在 prefab 重新 AddComponent 即可（HealthBarAnchor 子物体仍在） |
| AmmoUI | 弹药面板（v0.6.3）：武器色块+名 + 弹夹 x/y + 换弹进度细条（运行时构建，订阅 PlayerCombat 事件；无弹夹/默认近战隐藏） |
| SlotBarUI | 主 UI 槽位条（v0.7.2）：屏幕右下四槽（小技能/大招/武器技能/道具栏；技能三槽 v0.7.4 起由 SkillExecutor 每帧驱动——SetSkillDisplay 技能名+技能色 / SetSkillCooldown 文本秒数，数据缺失槽维持"—"，红闪提示施放失败）+ 道具栏上方背包 3 格（Button 点击与道具栏互换）；RuntimeInitializeOnLoadMethod 自举运行时构建，订阅 ItemInventory.OnChanged，数量角标 count≥2 显示、超 99 显示 99+；UiScale 常量整体缩放（CanvasScaler.scaleFactor，勿改根 RectTransform，自检 #23）；格子框美术 Assets/Art/UI/SlotFrame（v0.7.3 美术替换，固定路径加载，缺失退回纯色占位；打包需复制到 Resources/Art/UI/） |

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
| DamageResolver | 伤害结算静态入口（v0.7.0）：Deal(target, ctx) 单点收口；v0.7.1 +ApplyArmor 减伤甲纯函数（玩家/怪物共用一份实现：PlayerStats.ApplyArmorDamage 与 EnemyHealth.TakeDamage 均调它） |
| ProjectileData | 子弹配置 SO（v0.6.3）：速度/伤害/存活/半径/视觉类型/配色/目标层；资产在 Assets/Data/ |
| Projectile | 子弹（v0.6.3）：直线飞行 + Trigger 命中 IDamageable + 撞墙（Default 层）销毁 + 存活兜底 + 通用命中特效；v0.7.0 玩家子弹走 DamageResolver（owner 根查 PlayerStats，damageMul 映射 ctx.multiplier），敌人子弹原路径 |

### Enemy/ — 敌人侧
| 脚本 | 职责 |
|------|------|
| EnemyController | 移动/朝向门面、受伤闪烁、死亡处理；v0.6.3 死亡按 EnemyStats.manaOrbValue 掉法力球 |
| EnemyStats | 敌人属性（部分攻击字段已迁移 AttackData）+ ApplyFloorScale 楼层缩放（v0.5.4，只缩 HP，护甲不缩放 v0.7.1）+ manaOrbValue 击杀掉蓝（v0.6.3：普通 3/精英 8/Boss 20）+ 护甲三字段 maxArmor/armorReduceMul/armorLossMul（v0.7.1，默认 0/0/1=普通怪无甲，OnValidate 钳制 R≤0.9/L>0） |
| EnemyHealth | 敌人生命值（IDamageable），受击通知 AI + ScaleMaxHealth（v0.5.4）+ 减伤甲结算（v0.7.1：currentArmor/MaxArmor/独立 OnArmorChanged 事件/AddArmor 预留，TakeDamage 走 DamageResolver.ApplyArmor，无甲敌人全额扣血数值不变，ResetHealth 护甲回满） |
| EnemyCombat | 敌人攻击状态机 + 冷却；攻击触发判定 = AttackData.AttackRange + 0.3 缓冲（v0.6.0） |
| EnemyAI | Patrol/Chase/Attack/ReturnToPatrol 状态机；Update 决策 + FixedUpdate 统一写速度（v0.6.0 抖动修复），无用的 attackData 字段已移除 |
| TrainingDummy | 伤害测试木桩（v0_4 场景在用） |
| PatrolSystem | 巡逻点生成（EnemyAI 使用） |
| WorldSpaceHealthBar | 敌人头顶血条；v0.7.1 双条化：有甲敌人（精英/Boss）HP 条上方加钢灰护甲细条（#708090，高约 HP 条 1/3，canvas 向上加高，订阅独立 OnArmorChanged），玩家/无甲敌人零变化 |

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
| ClassData | 职业配置 SO：三属性上限 + 六维字段（v0.7.0：攻击/暴击率/暴伤/护甲双倍率 R·L，占位默认值同 PlayerStats）/职业色/可用武器列表；v0.7.4 +技能字段（skillBranches 分支表 / ultimateSkill 大招，本版资产未接线，null 走 SkillCatalog 兜底）；资产在 Assets/Data/Class/ |
| ClassCatalog | 职业资产目录（编辑器 AssetDatabase 加载，构建需 Resources/Class/） |
| PrepPedestal | 准备房间展台（职业选择台/武器展示台，运行时多色块视觉，E 交互；名签参数序列化可调） |
| PrepRoomPlacer | 三展台布置 + 武器展台刷新 + 初始武器自动归位（仅供准备场景，阶段 C 重构签名） |
| ClassSelectUI | 职业选择界面（TMP 屏幕空间）：选择→高亮→确认闪烁→ApplyClass→展台刷新；v0.7.0 职业按钮描述改六维数值行（HP/护甲/攻击/魔力/暴击%/暴伤×） |
| RunStateCarrier | 跨场景配置载体（DontDestroyOnLoad）：LastChosenClass/LastWeapon/HasLoadout；v0.7.4 +ChosenSkillBranchIndex 小技能分支索引（SetSkillBranch 局外写入、局内锁定，死亡保留与 LastChosenClass 同规则） |
| PrepRoomManager | 独立准备场景总控：房间视觉/展台/传送门/出生位/换武器归位订阅（v0.7.2 改置 storeOldWeaponInSatchel=false）；v0.7.3 地面运行时投放三种正式消耗包各 1 个（SpawnDemoItems），三包资产名清单与加载单点收口（ConsumableAssetNames / LoadConsumable / LoadRandomConsumable，宝箱与商店陈列共用） |
| PrepPortalInteractable | 准备场景进入地牢传送门：校验 HasLoadout → LoadScene |

### Weapon/ — 武器框架（v0.6.2 框架 / v0.6.3 完整实现）
| 脚本 | 职责 |
|------|------|
| WeaponData | 武器配置 SO（职业/行为类型/攻击引用/子弹引用/自疗量/蓄力规则与参数/弹夹射速/染色/图标；v0.7.4 +weaponSkill 武器技能引用，本版资产未接线，null 走 SkillCatalog 兜底）；资产在 Assets/Data/Weapon/ |
| WeaponInstance | 武器运行时状态（弹夹/换弹/蓄力计时），纯 C# 类 |
| WeaponBehavior | 行为基类 + 三派生分发（v0.6.3）：Melee → PlayerCombat 近战链；Ranged → Projectile 开火；SelfCast → 治疗自施法 |
| PlayerWeaponHolder | 玩家武器持有与装备入口：v0.7.2 换武器旧武器入 WeaponSatchel（storeOldWeaponInSatchel 可关，包满挤出者原地掉落可捡回）；OnWeaponChanged 事件（准备场景归位订阅）；Unequip 同步清空武器背包 |
| WeaponPickup | 武器拾取物（IPickupable）：职业校验"职业不符"拒绝，符合则 Equip；v0.6.3 地图掉落视觉 = WeaponVisualBuilder 小图标 + 职业色底板 |
| WeaponVisualBuilder | 运行时武器视觉（v0.6.3）：六武器模块化多色块手持视觉（含 Effect 蓄力发光部件）+ 地图掉落小图标 |
| ProjectileVisualBuilder | 运行时子弹视觉（v0.6.3）：箭矢/弩矢/能量弹/精灵弹四种子弹拼接 + 通用命中特效 + 共享白图/圆图 Sprite 缓存 |

### Item/ — 道具与背包（v0.7.2）
| 脚本 | 职责 |
|------|------|
| ConsumableData | 消耗品配置 SO：displayName/effectType(HP/Armor/Mana)/value/iconColor/占位图标；正式资产 Assets/Data/Item/ 下 Item_HealPack（HP+4）/ Item_ArmorPack（Armor+4）/ Item_ManaPack（Mana+40，占位数值待定稿；Consumable_Test 已删）；效果已接线（v0.7.3：UseActive → Health.Heal / ModifyArmor / AddMana） |
| ItemInventory | 玩家道具背包（纯数据+事件 OnChanged）：道具栏激活位 1 格 + 背包 3 格，同类叠加无上限；Add 分流（栏同类→栏空→包同类→包空位→满 false）；UseActive 扣数清零出槽；SwapWithBackpack 点击互换；PlayerController.Awake 运行时挂载 |
| ItemPickup | 消耗品拾取物（IPickupable）：E 拾取 → ItemInventory.Add，满则提示"背包已满"不消耗拾取物；运行时色块占位视觉 + Spawn 静态构建 |
| WeaponSatchel | 武器背包（1 格纯 C# 数据类，PlayerWeaponHolder 持有）：Store 返回被挤出者；死亡重开 Clear；本版无 UI |

### Skill/ — 技能框架（v0.7.4）
| 脚本 | 职责 |
|------|------|
| SkillType | 技能类型枚举（MeleeAoE 本版唯一实现；扩展位预留） |
| SkillData | 技能配置 SO：displayName/skillType/蓝耗/CD/伤害倍率/AOE 半径/占位色/等级（OnValidate 钳 ≥1）+ 等级数值表 damageMultiplierByLevel（空表=平直，GetDamageMultiplier 按 level 查表、越界回退基值，供 v0.7.6 天赋升级读）；资产在 Assets/Data/Skill/，占位数值【待补充·数值】 |
| SkillBranchData | 小技能分支表 SO（每职业一份，局外切换、局内锁定）：List<SkillData> branches，GetBranch 越界回退 0；切换入口 UI 未做【待补充】 |
| SkillCatalog | 技能资产目录（ClassCatalog 同模式：编辑器 AssetDatabase / 构建 Resources.Load("Skill/...")，打包需复制资产到 Resources/Skill/）：资产名清单单点收口；GetBranches/GetUltimate(ClassType) + GetWeaponSkill(WeaponData) 三入口（射手/法师未实装返回 null 并 Warning）；ClassData/WeaponData 接线值优先、null 走本目录兜底 |
| SkillExecutor | 技能执行器（玩家组件，PlayerController.Awake 运行时 Get-or-Add）：三槽装配（小技能=分支选中 ← RunStateCarrier.ChosenSkillBranchIndex / 大招=职业 / 武器技能=当前武器）、CD 计时、法力校验（TryConsumeMana）、按类型执行（MeleeAoE 旋风斩占位：OverlapCircleAll Enemy 层 → DamageResolver.Deal，baseAttack 只取角色攻击）；每帧推 SlotBarUI 技能名/CD 秒数，CD 中/法力不足红闪；订阅 OnWeaponChanged 武器技能槽整套替换（CD 清零独立）；表现=AttackIndicator 圆形灰显 0.2s + DOTween 缩放缓圈（SetLink） |

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
| InteractableSpawner | 交互物生成（散点冲突重试 / Row 一列陈列）；v0.7.3 商店房（RoomType.Shop）Row 收尾追加第二排三种消耗包各 1 个（ItemPickup.Spawn 运行时投放，免费占位与基座同规则） |
| SpawnPositionHelper | 生成位置合法性（距墙/距门/防重叠/重试上限） |

### Dungeon/Interaction/ — E 键交互物 + 拾取框架
| 脚本 | 职责 |
|------|------|
| Interactable | 交互基类（v0.6.1：walk-over → E 键 Interact()）：一次性触发 + OnConsumed 钩子 + 压暗已消耗态 |
| IPickupable | 可拾取物接口（v0.6.1 两段式拾取框架）：DisplayName + OnPickedUp |
| HealPickup | 治疗球拾取物（v0.6.1）：宝箱落物，按 E 拾取 +2HP，运行时补触发器 |
| ChestInteractable | 宝箱：三段式开箱动画；v0.7.3 奖励三权重 = 本职业随机武器 0.4 / 法力瓶 0.3 / 随机消耗包 0.3（序列化可调，消耗包走 ItemPickup.Spawn + PrepRoomManager.LoadRandomConsumable，资产缺失退回法力瓶；无职业兜底 HealPickup） |
| ShrineInteractable | 事件祭坛：随机 ±（治疗/受伤，运行时事件不进种子流） |
| SupplyInteractable | 商店补给：治疗球/护甲球/法力瓶（v0.6.3 +Mana 分支，免费占位） |
| PortalInteractable | 传送门（v0.5.4）：石块漩涡动效，按 E → RunManager.NextFloor |
| ManaOrb | 击杀掉落小法力球（v0.6.3）：walk-over 自动吸附（不占 E、不进拾取列表），AddMana 后飞入销毁 |
| ManaBottlePickup | 法力瓶（v0.6.3，IPickupable）：E 拾取 +40 法力；宝箱掉落 + 商店陈列（Supply_ManaBottle prefab） |

## 明确不在用（留在档案目录）
- `v0_2/CharacterInput.cs`、`v0_2/WarriorCharacter.cs` — v0.2 旧场景专用
- `v0_3/Enemy.cs`、`v0_3/WarriorAttack.cs`、`v0_3/Interfaces/IAttack.cs` — 已弃用
- `Framework/DamageSystem.cs`、`Framework/DetectionSystem.cs` — 未被任何脚本引用
