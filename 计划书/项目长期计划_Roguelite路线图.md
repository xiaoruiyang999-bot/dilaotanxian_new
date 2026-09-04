# 项目长期计划 —— Roguelite 内容路线图

> 状态：**已拍板 · 依照执行**（4 项决策已定，见第 5 节）
> 日期：2026-08-19（同日拍板决策点）
> 当前版本基线：v0.6.0（小地图）／核心循环闭环于 v0.5.4
> 关联文档：[项目进度报告](项目进度报告.md) / [项目框架文档](项目框架文档.md) / [v0.5.4.4.4_战斗判定与场景恢复修复](v0.5.4.4.4_战斗判定与场景恢复修复.md)

---

## 1. 一句话现状

**"能玩"已经成立，"想一直玩"还没有。** 打怪 → 清房 → 下楼 → 死亡重开的核心循环
（v0.5.4）+ 小地图（v0.6.0）已闭环且架构干净（事件驱动 / SO 数据驱动 / 可离线验证）；
但对照成熟 roguelite，**乐趣三支柱——成长感、目标感、反馈感——各缺一角**。

## 2. Gap 分析（现状证据 → 缺口）

| 维度 | 现状（已核实） | 缺口 |
|---|---|---|
| **局内成长** | 玩家只有固定 HP/护甲/移速；宝箱是占位奖励（治疗）；4 个 AttackData 全是敌人的，玩家无武器/技能数据 | 金币、掉落、商店、武器/技能、三选一升级、遗物——"打怪变强"的内循环整条缺失 |
| **目标感** | 无限爬层，无终点；Boss = 数值加强版普通怪 | 通关定义（最终 Boss）、Boss 技能与阶段、楼层主题轮换、死亡/通关结算画面 |
| **反馈感** | 有伤害飘字、攻击预警、受击闪烁；**全项目零音频代码**；无 hit-stop / 屏幕震动 | 音效/BGM、命中停顿、震屏、击杀演出——"打击感"三件套 |
| **操作上限** | 移动 + 单一攻击 | 闪避 Dash（无敌帧）、副手/技能键、手柄支持 |
| **局外沉淀** | 死亡 = 完全清零；无存档系统（RunManager 仅预留扩展点） | Meta 货币、永久升级树、角色解锁、存档与设置持久化 |
| **内容广度** | 5 种 AI 行为 × 2 词缀；7 房型（商店房有布局无内容） | 敌人视觉变体、词缀池、挑战房/秘密房、祭坛房事件多样化 |

## 3. 路线图（里程碑制，内部沿用 v0.x.y 小步递进）

> 排序原则：先让**每一秒的体验**变好（手感/反馈），再打通**收益循环**（成长/经济），
> 再建立**目标**（通关），最后做**留存**（Meta）——与《项目进度报告》v0.6 候选
> #2（技能装备）#3（商店经济）#4（Boss 技能）顺序一致，仅插入了反馈层。
> 每个里程碑开工时按项目惯例另建 `v0.x_操作文档.md` 记录当日细节；本节是任务总账。

### M1 · v0.6.x「手感与反馈包」（✅ 已完成，粒度：细）

**目标**：砍中敌人"手感明显变重"，游戏不再静音。

| # | 任务 | 挂点 / 改动 | 说明 |
|---|---|---|---|
| 1.1 | 闪避 Dash | `PlayerInputActions.inputactions` 加 `Dash` action（Space，Player map）；`PlayerController`：`dashSpeed/dashDuration/dashCooldown` 字段 + `OnActionTriggered` 分支 + `FixedUpdate` 中 Dash 状态覆盖移动速度（朝向 = 当前移动方向，无输入时用面朝方向） | 死亡重开（`Respawn`）时重置冷却与状态 |
| 1.2 | Dash 无敌帧 | `Health` 加 `public float iFrameUntil`（或 `bool Invulnerable`），`TakeDamage` 开头检查 | Dash 期间置位；与护甲吸收顺序无关，纯跳过 |
| 1.3 | 音频框架 | 新建 `Common/AudioManager.cs`：池化 AudioSource ×6、`PlaySFX(name)` / `PlayBGM(clip)`、SFX/BGM 两路音量 | 场景挂载同 MinimapSystem 模式（空对象，代码自建） |
| 1.4 | 5 个基础音效 + BGM | Kenney.nl CC0 音频包（决策②） | 命中 / 玩家受击 / 敌人死亡 / 开门 / 宝箱，BGM 单曲循环 |
| 1.5 | 音效挂点 | `WeaponHitbox.OnHit`、`Health.TakeDamage`、`EnemyController.OnEnemyDeath`、`Door.RefreshState`、`ChestInteractable` | 全部一行式调用，不侵入逻辑 |
| 1.6 | Hit-stop | 静态工具 `HitStop.Request(0.03~0.05f)`：`Time.timeScale=0` + unscaled 计时恢复 | 挂玩家命中（轻）与玩家受击（可选更轻）；Boss 死亡大停顿留给 M3 |
| 1.7 | 屏幕震动 | `CameraFollow` 加 `Shake(float intensity, float duration)`：随机偏移指数衰减，叠加在 LateUpdate 跟随位之后 | 命中轻震 / 玩家受击重震；幅度参数进 Inspector |
| 1.8 | 还债：受击闪烁变白 | `EnemyController.OnHitFlash` 先 `StopCoroutine` 旧协程再启动（同 `DestructibleObstacle` 的正确写法） | 遗留清单 #1 |
| 1.9 | 还债：Windup 取消进冷却 | `EnemyCombat.UpdateWindup` LOS 丢失取消时 `CancelAttack(true)` 或单独短冷却，消除"蓄力→取消"抖动 | 遗留清单（v0.5.4.4.2 遗留） |
| 1.10 | 还债：感知 GC | `EnemyPerception` 三处查询改 NonAlloc 静态缓冲 + `GetTargetPoint` 缓存 Collider | 遗留清单 #2，敌人多时的掉帧主嫌疑 |

**验收清单**：
- [x] Space Dash：短位移 + 无敌帧内敌人攻击不掉血 + 有冷却
- [x] 命中有声有停顿，受击有震屏，不再静音
- [x] 连续受击的敌人不再永久变白；远程敌人不再反复"蓄力取消"抖动
- [x] 8 敌同屏帧率平稳（NonAlloc 化完成）

### ✅ 计划外支线 · v0.6.0–v0.6.10「狼人角色系统」（2026-08 完成，主线暂停期间插入）

用户自选需求，属 M5"内容量"范畴的角色内容提前落地：开局即狼 / T 键兽化（变身动画 + 逐帧膨胀 +
粒子特效 + 血量 ×1.5 等比）/ 走路待机全动画态 / 攻击体系同源放大。详见
[v0.6.8_狼人角色系统开发文档](v0.6.8_狼人角色系统开发文档.md)。附带成果：UnitySkills 工具链接入
（REST 直操编辑器）、三层红线文档体系（AGENTS.md / project-dev-rules skill / 开发必读）、素材管线三工具。
待兽化走路素材已补（v0.6.10）；剩余尾巴：变身右版、`#if UNITY_EDITOR` 帧加载打包前资源化、兽化数值（接 M2）。

### M2 · v0.7.x「收益循环」（✅ 已完成 2026-08-22，详见 [v0.7.1_M2完成_收益循环](v0.7.1_M2完成_收益循环.md)；原计划粒度：细）

**目标**：打怪→捡钱→消费→变强的内循环成立，每局 build 开始有差异。

| # | 任务 | 挂点 / 改动 | 说明 |
|---|---|---|---|
| 2.1 | 钱包 + HUD | `PlayerStats` 加 `Coins`（或新 `PlayerWallet` 组件）：`Add/Spend` + `OnCoinsChanged` 事件；HUD 金币数字（纯代码 Text，风格同小地图） | 存量架构：事件驱动，UI 只订阅 |
| 2.2 | 金币掉落物 | 新 `CoinDrop` prefab（小圆 + Rigidbody2D 弹跳散落 + 磁吸玩家触发器吸收） | 数量 = 敌人成本基准 × 楼层系数；掉在 `dungeonRoot` 下随层清理 |
| 2.3 | 掉落挂点 | `EnemyController.OnEnemyDeath`（敌人侧统一入口，不必动 Health/EnemyHealth 两套） | 精英/Boss 掉率翻倍参数留 Inspector |
| 2.4 | 商店第一版 | 新 `ShopInteractable : Interactable`（`ApplyEffect` = 尝试购买 + 余额不足飘字反馈）；商店房货架生成挂 `RoomContentProfile`/Spawner 链 | 货品 v1：治疗 / 护甲 / 升级券（2.6） |
| 2.5 | 宝箱真奖励 | `ChestInteractable.ApplyEffect` 改为消费 `LootTable` SO（金币/补给/升级券权重）；`healAmount` 降级为表中一项 | 注释中预留的挂点正好接上 |
| 2.6 | 局内升级三选一 | 新 `UpgradeManager`（timeScale=0 弹面板，纯代码 Canvas 同小地图风格）+ `PlayerUpgradeDef` SO（伤害% / 攻速% / 移速% / 最大HP / 护甲上限 / Dash CD%） | `PlayerStats` 扩展 `damage/attackSpeed/cooldownMultiplier`，`PlayerCombat`/`WeaponController` 消费；触发源：升级券（宝箱/商店/精英房保底）；本局 build 列表死亡清空 |
| 2.7 | 还债：宝箱奖励被切层吞 | `ChestInteractable` 开盖动画的 `OnComplete` 改为立即结算 + 动画纯视觉 | 遗留清单 #6，动了宝箱顺手修 |
| 2.8 | Framework 死代码决策 | `Framework/DamageSystem`（暴击/护甲公式 TODO）：设计 2.6 数值时决定启用或删除 | 遗留清单，避免两套公式 |

**验收清单**：
- [ ] 杀敌掉金币、可拾取、HUD 实时更新
- [ ] 商店能买东西、钱不够有反馈；宝箱开出不同奖励
- [ ] 拿到升级券暂停三选一，效果当局生效且死亡清零
- [ ] 一局内能说出"我这局走了什么 build"

### M3 · v0.8.x「Boss 与目标」（✅ 已完成 2026-08-22，详见 [v0.8.1_M3_Boss与目标](v0.8.1_M3_Boss与目标.md)）

**目标**：玩家能说出"我要打到第 9 层"，Boss 战有记忆点。决策①：9 层通关制。

| # | 任务 | 挂点 / 改动 | 说明 |
|---|---|---|---|
| 3.1 | 楼层主题轮换 | 新 `FloorTheme` SO（地板 tint / SpawnTable 覆盖 / BGM / 主题名）；`DungeonManager.Generate` 按 `FloorNumber` 取主题（1-3 / 4-6 / 7-9 三套） | `RunManager` 层号透传已就位；`RoomTypeConfig.floorTint`、`SpawnTable` 均为现成注入点 |
| 3.2 | Boss 技能阶段化 | Boss 专属 `EnemyBehaviorConfig` 扩展：血量阈值切换招式池（阶段 2~3）；招式复用现有弹幕/冲锋/召唤能力 | 三阶段攻击预警框架天然支持；Boss 死亡加大停顿 + 大震屏（M1 接口） |
| 3.3 | 最终 Boss（第 9 层） | 专属 Boss prefab + 击杀 → 通关流程 | 主题 3 的压轴 |
| 3.4 | 通关结算 + 无尽模式 | `RunManager`：通关面板（统计：击杀/金币/用时/层数）→ 解锁无尽 flag → 传送门变为无尽入口（难度继续按层增长） | 结算面板第一版，M4 Meta 结算复用 |
| 3.5 | 小地图主题适配 | `MinimapController` 房间配色不变，标题行加主题名 | 顺带 |

**验收清单**：
- [ ] 1-3 / 4-6 / 7-9 层色调、敌池、BGM 明显不同
- [ ] Boss 有阶段变化，玩家能描述"它半血后换了招"
- [ ] 第 9 层通关出现结算，可继续无尽模式

### M4 · v0.9.x「Meta 进度与存档」（✅ 已完成 2026-08-23，详见 [v0.9.0_M4_Meta进度与存档](v0.9.0_M4_Meta进度与存档.md)；含跳过项决策）

**目标**：死亡有"赚到了"的感觉而非纯清零——rogue-**lite** 成型。

| # | 任务 | 挂点 / 改动 | 说明 |
|---|---|---|---|
| 4.1 | 存档系统 | 新 `SaveSystem`：JSON + `Application.persistentDataPath`，版本号 + 字段迁移 + 损坏兜底（重命名坏档另存） | `RunManager` 预留扩展点正式接上 |
| 4.2 | Meta 货币 | 死亡/通关结算按层数与击杀折算"魂"，**跨局保留** | 复用 M3 结算面板 |
| 4.3 | 永久升级树 v1 | `MetaUpgrade` SO ×3~5（初始 HP+ / 开局金币+ / 商店折扣 / Dash 充能+1），结算页消费 | 只做数值节点，树状 UI 不做 |
| 4.4 | 中断续玩（第二优先） | 序列化 `floor/seed/钱包/build 列表`，重进游戏恢复到**当前层重生成**（seed 可复现性已保证；房内进度不存） | 可裁剪：若手动成本高，v1.0 再做 |
| 4.5 | 还债前置：Health/EnemyHealth 合并 | 存档要统一实体状态序列化，两套平行类先合并 | `Room`/`EnemyController`/`WeaponHitbox` 引用点较多，留足回归测试 |
| 4.6 | 最小自动化测试 | SaveSystem JSON 往返测试（编辑器下跑） | 项目首个测试，管线就位后再谈覆盖面 |

**验收清单**：
- [ ] 死亡后魂余额保留，下一局开局生效永久加成
- [ ] 关游戏重开，Meta 数据完好；坏档不崩溃
- [ ] （若做续玩）中断后重进回到中断楼层

### M5 · v1.0「内容量与打磨」（⬅ 最后一个里程碑，粒度：粗，开工前再细化）

**目标**：从"完整骨架"到"值得发售的游戏"。

| # | 任务 | 挂点 / 依赖 | 说明 |
|---|---|---|---|
| 5.1 | 武器系统（玩家侧） | 玩家/敌人已共用 Combat 框架——做玩家 `AttackData` 资产 + 宝箱/商店掉武器 + 切换键 | M2 升级系统的自然延伸 |
| 5.2 | 新房型：挑战房 / 秘密房 | `RoomClearCondition` 扩展（波次）；`DungeonBuilder` 连接生成加隐藏门 | 挑战房 = 高风险高奖励 |
| 5.3 | 词缀池扩展 + 敌人变体 | `EnemyAffixConfig` / `EnemyStats` 现成结构 | 配置工作为主 |
| 5.4 | 教程 / 暂停 / 设置 | 首局教学（固定 seed + 提示牌）；暂停菜单（timeScale + 继续重开）；音量/键位重映射（Input System rebinding） | |
| 5.5 | 手柄支持 | Input System 天然支持，补 UI 导航与图标提示 | 决策④顺延到此 |
| 5.6 | 平衡 pass | 掉落表/升级池数值纳入 seed 验证管线；还债 `PatrolSystem` 随机源统一（复现性） | |
| 5.7 | 发布 checklist | 性能（同屏实体上限）、分辨率适配、图标/命名、商店页素材 | 决策③：v1.0 后统一换皮 |

### v1.0+ 长线池（不承诺顺序）
新角色、周挑战种子、云存档、Steam 成就、美术素材全面替换（决策③：v1.0 后统一换皮）

## 4. 技术债还款计划（与里程碑任务表联动）

| 债务 | 建议还款时机 | 对应任务 |
|---|---|---|
| v0.5.4.4.4 遗留 13 项中低危 bug | 影响哪个里程碑就先还哪个 | M1.8 闪烁变白 / M1.9 Windup 冷却 / M2.7 宝箱吞奖励；EnemyHealth 负伤害随 M2 数量增多时顺带 |
| EnemyPerception 每帧 GC 分配（RaycastAll 无 NonAlloc） | M1 手感包 | M1.10（敌人多时的掉帧主嫌疑） |
| Health / EnemyHealth 平行重复 | M4 存档前 | M4.5（掉落已改挂 `EnemyController.OnEnemyDeath`，M2 不再强制合并；存档要统一实体状态序列化，届时合并并回归） |
| Framework 层零引用死代码（暴击/护甲公式 TODO） | M2 数值设计时 | M2.8（启用或删除，避免两套公式） |
| PatrolSystem 用 UnityEngine.Random 破坏 seed 确定性 | M5 平衡 pass 前 | M5.6（seed 验证需可复现） |
| 无自动化测试 | M4 | M4.6（SaveSystem JSON 往返，项目首个测试） |

## 5. 决策点（✅ 已全部拍板，2026-08-19）

1. **✅ 通关定义**：**9 层通关制**——3 主题 × 3 层，第 9 层最终 Boss，通关后解锁无尽模式。M3 按此设计；楼层主题轮换（M3）即 3 个主题的落地载体。
2. **✅ 音频资产来源**：**免费素材包**（Kenney.nl / OpenGameArt，CC0）。M1 落地 AudioManager 时同步选包；优先无版权风险的 CC0 素材，BGM 单曲循环起步。
3. **✅ 美术方向**：**v1.0 后统一换皮**——几何+染色风用到 v1.0，中途不替换素材、不分散精力；v1.0 后作为独立里程碑统一替换。
4. **✅ Dash 键位**：**Space**。M1 实现时接 Input System（与 Move 同一份 PlayerInputActions 资产加 action），手柄支持排入 M5 设置项一起做。

---

> 维护约定：每完成一个里程碑，在《项目进度报告》记一条，本文档勾掉对应条目；
> 决策点拍板后把结论写回第 5 节并标日期。
