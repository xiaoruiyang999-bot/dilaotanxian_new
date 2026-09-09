# AGENTS.md — 本项目开发指令

> v1.2 玩法基线（2026-09-08）：2D 俯视角房间制 Roguelite。房间内二维自由移动与战斗；宏观 DungeonGraph 为从左到右的 DAG，只通过路线地图分叉/汇聚，物理房间默认左入右出。战士退役；原角色与职业功能合并为“职业角色”，首发狼人，后续法师、弓箭手。选择职业角色同时确定外观、属性、武器池、技能、职业资源、动画和专属 Build。武器画入角色 Sprite，身体碰撞与 Attack Hitbox 分离。新动画统一使用 Unity Animation/Animator。攻击方向仍待灰盒验证，不得擅自定为仅左右或 360°。

完整设计真源：计划书/v1.2.0_俯视横向单向DAG地牢_GDD与开发计划.md。

项目使用 Unity 6 + URP 2D + Input System + DOTween。现有脚本在 Assets/Scripts/now_use，编译为 Game 程序集。当前场景与代码仍以 v1.1.x 旧系统为主，只作为迁移基线，不代表 v1.2 已实现。

**凡写 C#、改场景/Prefab/导入资产或操作 Unity Editor，必须先加载 project-dev-rules skill。** 本文件为摘要，完整工程规则见该 skill 与根目录《开发必读_核心信息整合.md》，三者涉及红线的修改必须同步。

## 最高优先级规则

1. **单一职业角色真值**：新版只使用 PlayableCharacterId；禁止继续并存独立 CharacterId 和 ClassId。
2. **数据与运行对象分离**：DungeonGraph/Node/RunState 为纯 C# 数据；Generated、Discovered、Visited、Completed 不依赖房间 GameObject Active。
3. **攻击同源**：预警、动画窗口、Hitbox、伤害、位移与反馈消费同一 AttackDefinition；可见武器像素不是判定真源。
4. **碰撞分责**：Body Hurtbox、Movement Collider、Attack Hitbox 独立。武器烘入 Sprite 不得扩大身体碰撞。
5. **新动画主链**：Unity Animation/Animator + Override Controller；FrameAnimator 与 WeaponPivot 停止扩建，迁移完成后再安全删除。
6. **DAG 与房间**：DAG 上下分支不生成上/下门；实际房间左入右出。RoomPlan 先验证后一次绘制，入口到出口按玩家碰撞体口径可达。
7. **Trigger 规则**：Trigger 只参与逻辑事件；LOS、投射物、移动探测跳过 isTrigger，实体墙才阻挡。
8. **零 GC 热区**：高频物理查询 NonAlloc + 复用缓冲；缓存 GetComponent；避免 Update 中 LINQ/字符串分配。
9. **生命周期**：事件订阅/退订配对；协程/Tween 互斥并绑定对象；玩家死亡走 Respawn，新状态必须重置。
10. **Prefab 与 YAML**：组件写入 Prefab 本体；外部改场景后 Reload；YAML 手术先备份、感知行尾、验证 fileID 双向引用并重载。
11. **资源导入**：Sprite Mode、PPU、Pivot、帧尺寸显式统一；Editor-only 资源扫描在构建前资源化。
12. **调试纪律**：不移动相机或瞬移玩家伪造测试，不保存未授权场景，不留 Debug_ 对象；场景中只有一个激活 Player。

## 迁移顺序

攻击方向原型 → 职业角色合并 → 狼人 Animator 战斗 → 横向房间/镜头 → DAG 数据/地图 → 节点房间闭环 → Build/经济 → 敌人/Boss → 旧链清理。

任一阶段保持可运行入口。旧系统只有在新链通过测试并清除引用后才能退役。

## 文档惯例

- 玩法与范围只回写 v1.2.0 GDD。
- 功能/修复完成后新增 v1.2.x_主题.md，记录实际改动、验证和遗留项。
- 工程教训回写《开发必读_核心信息整合.md》和 project-dev-rules。
- 里程碑摘要在《计划书/项目长期计划_Roguelite路线图.md》。
- 《往期版本文档》只读，不作为当前实现指令。

