---
name: project-dev-rules
description: 本 Unity 地牢 Roguelite 项目的开发红线与架构规则（写代码前必读）。凡任务涉及以下任一情况必须先加载本 skill——编写/修改任何 C# 脚本、改动场景或 Prefab、导入图片/序列帧资产、物理检测(LOS/投射物/碰撞)、战斗判定或预警、敌人 AI/死亡流程、玩家状态、YAML 手术、使用 UnitySkills 操作编辑器。即使只是"加个小功能/改一行"也要过一遍红线，历史上 90% 的事故来自小改动踩了已知坑。
---

# 项目开发红线与速查（真源：根目录《开发必读_核心信息整合.md》，更新须同步）

## 架构铁律
1. **分层禁跨层**：输入→决策(AI/Room状态机)→执行(Combat状态机)→判定(WeaponHitbox)→数据(AttackData SO)→表现/UI。表现层组件禁止逻辑（AttackIndicator 类注释即合同）。
2. **事件驱动**：System.Action 事件解耦，订阅/退订必须配对（OnEnable/OnDisable 或 OnDestroy）。
3. **SO 同源原则**：预警=判定=武器视觉消费同一份 AttackData；判定几何唯一真源 `WeaponHitbox.CurrentBoxGeometry`（长=AttackRange×lossyScale，宽=weaponWidth×lossyScale 的 OverlapBox，从攻击者中心沿攻击方向延伸）。预警用 Box 形状 + SetBoxSize 消费它，不得自算。
4. 脚本只用 `Assets/Scripts/now_use/`（v1.0.4 起编译为 **Game** 程序集；DOTween 模块为 **DOTween.Modules** 程序集）。**asmdef 按名字引用预定义程序集（Assembly-CSharp）静默无效**——测试/工具程序集要引游戏代码必须走 Game。v0_1~v0_4 归档已删除（历史在 git），勿捞回。

## 写代码前 Checklist
- [ ] 属于哪一层？跨层了吗？
- [ ] 新事件订阅有配对退订？协程互斥（先 Stop 旧的）？
- [ ] 每帧零 GC：物理查询 NonAlloc+静态缓冲，GetComponent 缓存
- [ ] **Trigger 只参与逻辑事件**：LOS 射线/投射物/移动探测一律 `isTrigger` 跳过；实体碰撞（墙 TilemapCollider）才阻挡。投射物命中无 IDamageable 时：Trigger 穿透、非 Trigger 挡弹销毁
- [ ] 敌人 OnDisable 用 `EnemyHealth.IsDead` 区分死亡（销毁指示器）vs 休眠（只 Hide 复用）；脱离父物体的对象防孤儿泄漏
- [ ] 玩家对象从不销毁（死亡=ResetHealth+Respawn）；**新增玩家状态字段必须加进 Respawn 重置**
- [ ] GetComponentsInChildren 递归收集时确认是否排除特定子树（狼人视觉被误藏教训）
- [ ] 运行时构建 UI 文本：照 ClassSelectUI.CreateText 模式（无参 GameObject + 单次 AddComponent<TextMeshProUGUI> + 先 text 后 font=TMPFontProvider.Font）；**禁止把组件塞进 GameObject 构造参数**（双 TMP 组件 → TMP 内部 NRE），也勿用 LegacyRuntime.ttf 内置字体（GetBuiltinResource 运行时抛 NRE）
- [ ] 720px 序列帧默认 7.2 世界单位，需 scale/PPU；`#if UNITY_EDITOR` 便利代码打包前资源化
- [ ] 完成后 UnitySkills `script_get_compile_feedback` 验证（Domain Reload 断连=等待重试）

## 踩坑红线（按代价排序）
- **R1** prefab 实例上 component_add 会被 scene_load 丢弃 → 组件进 prefab 本体（`prefab_apply_overrides`）
- **R2** YAML 手术：备份→**行尾感知**（场景 LF / Player.prefab **CRLF**）→fileID 查重→组件三处双向（块+GO m_Component+父TF m_Children）→删根对象同步 SceneRoots→`scene_load` 重载验证。UnitySkills 属性设置用公开名（`sprite` 非 `m_Sprite`），资产引用走 `assetPath=`
- **R3** `textureType=Sprite` 默认 Multiple 自动切片（透明碎片假象）→ 必须显式 `spriteMode=Single`；meta 的 internalIDToNameTable 应为 `213: 21300000 → 文件名`
- **R4** 外部改场景后必须 Reload 再 Play（旧内存副本假象：移动键全失灵/双玩家抢键盘）
- **R5** Play 模式编辑是运行时的（退出还原）；Domain Reload 断服务=等待，长时不恢复去面板重开
- **R6** 层现状：墙/玩家/交互物全在 Default，Obstacle=可破坏障碍；AttackData.obstacleLayer 指 Obstacle
- **R8** 三态攻击：Windup 预警（脱离父物体防跟随）→ Active 判定 → Recovery；远程预警线 Windup 内每帧跟手 + LOS 门控（丢失超 grace 取消进半额冷却）
- **R9** 变色协程：互斥 + 基线色持久缓存（勿从 sr.color 现读）
- **R10** 同帧多命中音效用 PlayOneShot；**R11** 无敌帧 `Health.GrantIFrames`（Time.time 口径，hit-stop 期间冷却暂停属预期）
- **R13** 集合引用比较陷阱：FrameAnimator.Play 复制新 List，外部引用比较恒不等→每帧重播卡帧 0。跨对象状态跟踪用枚举/标记
- **R14** Play 中改代码不热生效：editor_stop → asset_refresh 等编译 → editor_play；日志验证看格式特征（新增字段出现=新代码生效）
- **R15** 战斗房关门不得直接消费 `OnTriggerEnter2D`（只代表刚相交）：Enter/Stay 中须等玩家 `Collider2D.bounds` 完整落入 `Room.Bounds` 再 `Room.Enter()`，否则门会在角色横跨门洞时启用碰撞夹人；固定内缩量不等价于完整进入
- **R16** 房内生成只按最终 `RoomPlan` 绘制一次：Carved 深层格必须撤销 groundCells/Floor/Walls；每次 Shape 重试从 baseProtect 克隆，禁止轮廓保护跨尝试累积；障碍必须在临时集合中形成完整小岛候选，经格级+玩家口径验证和评分后一次提交，禁止增量回滚误删旧格；所有内容落点须先消费最终 `RoomPlan.SpawnCells`（3×3 无墙可走地面），再做距门与 NonAlloc 物理检测（v1.1.46）

## 速查
- 键位（v0.7.5）：WASD / 左键攻击 / E=交互·拾取 / F=小技能 / Q=大招 / R=武器技能 / C=用道具 / Esc=暂停（Dash/Sprint 已下线；狼人 T 已随 v0.5 体系退役）
- 事件：`DungeonManager.OnGenerated`（楼层重建链路）/ `Room.OnRoomEntered/OnRoomCleared` / `Health.OnDeath` / `WeaponHitbox.OnHit`
- 反馈：命中三件套挂 WeaponHitbox（"hit"+HitStop0.03+轻震）；受击在 Health（"hurt"+重震）；音效 `AudioManager.PlaySFX(id)` 未配置静默
- 狼人=可选角色外形（v1.0.6，与职业独立）：ClassSelectUI 外形行 → RunStateCarrier.ChosenCharacter → FrameAnimator.SetWerewolfVisual（左右帧组不 flipX，Resources/Art/Characters/Werewolf）；旧变身系统已退役，Beast/Transform 素材未接入；素材源工具在 `E:\WeGameApps\The animation of Unity\狼人\`
- 门禁：EditMode 测试 `Assets/Tests/EditMode/`（Missing Script 扫描+ApplyArmor+地牢 seed），提交前必跑；运行时加载资产放 `Assets/Resources/`（路径=编辑器路径镜像）；正式流程 Build Settings 收口为 v0_7_PrepRoom→v0_7_ClassWeapon
- 遗留债清单见 v0.5.4.4.4 文档第 4 节；里程碑任务总账见《项目长期计划_Roguelite路线图》
