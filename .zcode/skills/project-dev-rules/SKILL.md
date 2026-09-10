---
name: project-dev-rules
description: 本 Unity 俯视横向单向 DAG Roguelite 的开发红线。凡编写或修改 C#、场景、Prefab、Animator、Sprite、物理判定、战斗、AI、房间、DAG、玩家状态、YAML，或使用 Unity Editor 自动化时必须先加载。
---

# V2 项目开发红线

真源：

- 玩法与范围：计划书/无名之地_完整游戏开发文档_V2.md
- 工程规则：开发必读_核心信息整合.md
- 摘要：AGENTS.md

## 1. 先确认当前边界

- 房间内上下左右自由移动；宏观 DAG 单向向右。
- DAG 分支不映射为实际上门/下门。
- 角色与职业已合并为职业角色；战士退役，首发狼人。
- 武器视觉烘入角色 Sprite；Attack Hitbox 和身体碰撞独立。
- 新动画只扩展 Unity Animation/Animator。
- v2.0.1 首轮只做左右攻击灰盒：键鼠按鼠标相对角色的 X 正负定向，Y 不参与；不同时开发四方向版本，未确认手感前不量产最终攻击动画。
- now_use 中旧 Character/Class、四向网格房间、WeaponPivot、FrameAnimator 是迁移基线，不得当作新目标继续扩建。

## 2. 写代码前 Checklist

- [ ] 本任务对应 GDD 的哪个版本、模块和验收条件？
- [ ] 是否触及未定案设计？若是，范围是否仅限原型？
- [ ] 能否保持当前可运行入口？
- [ ] 新真值放在纯 C#、ScriptableObject、场景还是运行组件？是否唯一？
- [ ] 输入、决策、执行、判定、数据、表现是否分层？
- [ ] 高频路径是否零 GC，引用是否缓存？
- [ ] 新状态是否有 New Run、Respawn、Room unload 重置？
- [ ] 完成后如何编译、自动测试和 Play 验证？

## 3. 数据与架构

1. 新版职业角色只存 PlayableCharacterId。PlayableCharacterDefinition 统一外观、属性、武器类型、资源、技能、动画和奖励标签。
2. DungeonGraph、DungeonNode、RunState、Build、奖励 Seed 是纯 C# 可序列化数据。
3. 节点 Generated、Discovered、Visited、Completed 不从 GameObject.activeSelf 推断；Active 属于 RoomSession。
4. 静态职业/武器/遗物/攻击/敌人/模板配置使用 ScriptableObject；存档使用版本化 DTO。
5. 模块保持六个：Bootstrap/GameFlow、DungeonGraph、RoomRuntime、Combat、Build/Economy、Presentation/UI。
6. MonoBehaviour 保持薄；纯规则不依赖 Unity 场景，便于 EditMode 测试。
7. 跨模块用显式依赖或事件；禁止新的万能 Manager 和隐式全局 Find。

## 4. 战斗与 Animator

1. AttackDefinition 是前摇、有效帧、后摇、方向、判定几何、位移、伤害和反馈的同源数据。
2. Body Hurtbox、Movement Collider、Attack Hitbox 分责；Sprite 中画出的武器不参与身体碰撞。
3. Animator/Animation Event 只发窗口信号，不计算伤害、不直接改 Build。
4. 每个攻击 Clip 必须包含标准的 AttackStart、HitboxOpen、HitboxClose、AttackEnd，导入或测试时验证完整性。
5. 同武器模组共享 Controller 和时序；用 Animator Override Controller 替换 Clip。
6. FrameAnimator 与 WeaponPivot 只作旧场景兼容。在新 Animator 链接管、引用清零并回归通过前不删除。
7. 同帧多命中音效限流；变色/停帧/震屏协程或 Tween 必须互斥并绑定生命周期。

## 5. 物理与性能

- Trigger 只参与逻辑事件。LOS、投射物、移动探测跳过 isTrigger；非 Trigger 实体碰撞才阻挡。
- 高频查询使用 Physics2D NonAlloc API 和静态/复用缓冲。
- Update/FixedUpdate 中不使用 LINQ、临时 List、字符串拼接或反复 GetComponent。
- 玩家对象死亡走 Respawn；新增字段必须加入重置。
- 敌人/房间休眠与死亡分开；OnDisable 不等价于死亡。
- 只激活当前房间的敌人和表现，离开 RoomSession 时取消订阅、协程和 Tween。

## 6. DAG、房间与生成

1. DAG 边只指向更高 LayerIndex；唯一 Boss 汇点；所有节点可达 Boss；同 Seed 可复现。
2. 一个节点加载一个实际横向房间，默认左入右出。
3. 先生成最终 RoomPlan，再一次绘制。生成重试使用独立临时集合，失败不得污染。
4. 入口到出口必须按玩家 Collider2D 尺寸验证可达。
5. 障碍整岛候选通过格级、玩家口径和评分后一次提交，禁止增量回滚误删旧格。
6. 内容从最终 SpawnCells 取点，满足净空、距门、距墙，再做 NonAlloc 物理检测。
7. RoomTrigger 只有在玩家世界 Collider2D.bounds 完整进入 RoomBounds 后才能激活关门。
8. Camera Bounds 只绑定当前 RoomSession；Transition 后换 Bounds，震屏不能露出地图外。

## 7. Unity 资产与场景红线

- Prefab 实例上临时 component_add 可能被重载丢失；正式组件加入 Prefab 本体并 Apply/Reload 验证。
- 外部修改 .unity/.prefab 后必须 Reload，再检查编译、Hierarchy、唯一 Player，最后 Play。
- Sprite 导入显式设置 Single/Multiple、PPU、Pivot、Filter、Compression；序列帧必须统一。
- 720px 图片按默认 PPU 可能达到 7.2 世界单位，必须用目标世界尺寸核对。
- Editor AssetDatabase 便利代码打包前必须转换为 Resources、Addressables 或显式索引。
- YAML 手术：备份 → 行尾感知 → fileID 查重 → 组件块/GO 列表/父子/SceneRoots 双向同步 → Reload 验证。
- Unity 属性设置使用公开字段名和真实资产路径，不猜序列化内部名。

## 8. UI 与输入

- UI 只呈现和发送意图，不直接写节点完成、金币、伤害或职业真值。
- V2 MVP 键位：WASD、左键攻击、Space 闪避、E 交互、F 小技能、Q 大招/兽化、Tab 地图、Esc 暂停。
- 旧 T 兽化入口停用；R 武器技能与 C 道具键不进入首轮 MVP，是否恢复须在相应系统设计时确认。
- 运行时 TMP 对象避免重复添加组件；字体和资源必须有构建态来源。

## 9. 调试纪律

- 调试默认不移动相机、不改 Camera target、不瞬移玩家、不保存场景。
- 若用户授权瞬移，先记录并在结束时复位。
- 临时对象使用 Debug_ 前缀并在结束时清理。
- 外部场景修改后 Reload；确认 Console 无编译错、只有一个激活 Player。
- Play 中修改的运行时状态退出会还原，不把它误判为持久改动。
- 代码变更后停止 Play、刷新编译、重新 Play，以日志或行为特征验证新代码生效。

## 10. 完成门禁

- [ ] 编译无错误，Console 无新增红错。
- [ ] EditMode：DAG 无环/可达/Seed、节点状态、伤害公式、奖励职业过滤、Missing Script。
- [ ] Play/人工：职业角色选择、房间进入完成、Room unload、死亡重开、Camera Bounds。
- [ ] 场景中无 Debug_、临时 Player、错误 Camera target 或未说明 dirty。
- [ ] 版本实施记录已新增；玩法决策回写 GDD。
- [ ] 若新增工程教训，已同步开发必读、AGENTS.md 与本 skill。

