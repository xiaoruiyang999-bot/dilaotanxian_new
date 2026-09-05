# AGENTS.md — 本项目开发指令（ZCode 每次会话自动加载）

2D 地牢 Roguelite（Unity 6 + URP 2D + Input System + DOTween），脚本在 `Assets/Scripts/now_use/`（编译为 **Game** 程序集，v1.0.4 起；asmdef 引用 Unity.InputSystem/Unity.TextMeshPro/DOTween.Modules）。正式流程：`v0_7_PrepRoom`（启动）→ `v0_7_ClassWeapon`（地牢）；v0_5_Dungeon 为旧直连测试场景。v0_1~v0_4 归档与 Framework/ 死代码已于 v1.0.2 删除，历史在 git，勿捞回。

**写代码或改资产的任务开始时，必须先调用 `project-dev-rules` skill 加载完整红线**（本文件只是摘要层；skill 是全量层；根目录《开发必读_核心信息整合.md》是人读真源，三者更新需同步）。最高优先级规则摘要：

1. **同源原则**：预警=判定=武器视觉消费同一份 AttackData；判定几何唯一真源是 `WeaponHitbox.CurrentBoxGeometry`。
2. **Trigger 只参与逻辑事件**：LOS 射线/投射物/移动探测一律跳过 isTrigger；实体碰撞（墙）才阻挡。
3. **Prefab 实例上 component_add 会被 scene_load 丢弃**——组件要进 Player.prefab 本体（prefab_apply_overrides 路径）。
4. **YAML 手术**（场景 LF / Player.prefab CRLF）：备份→行尾感知→fileID 查重→组件三处双向（块+GO列表+父TF children）→SceneRoots 同步→scene_load 重载验证。UnitySkills 设图片导入必须显式 `spriteMode=Single`（默认 Multiple 自动切片=透明碎片）。
5. **物理查询零 GC**：NonAlloc+静态缓冲；协程互斥+基线色缓存；玩家对象从不销毁（死亡走 Respawn，新增状态字段必须在其内重置）。
6. **720px 序列帧默认 7.2 世界单位**，需 scale/PPU 校正；`#if UNITY_EDITOR` 的 AssetDatabase 便利代码打包前要资源化。
7. **外部改场景后必须 Reload 再 Play**；调试遵守《开发调试规范》（禁动相机/瞬移玩家/留 Debug_ 对象）。
8. 键位（v0.7.5）：WASD 移动 / 左键攻击 / E=交互·拾取 / F=小技能 / Q=大招 / R=武器技能 / C=使用道具 / **T=兽化变身**（仅狼人角色，v1.0.9 还原）/ **Esc=暂停·关UI**（Dash/Sprint 已下线）。反馈挂点：命中在 WeaponHitbox（音效+停帧+轻震）、受击在 Health（音效+重震）、敌死在 EnemyHealth、开箱在 ChestInteractable；音效走 `AudioManager.PlaySFX(id)`——AudioManager 全局常驻（BGM 跨场景不打断），Kenney 占位表+DungeonBGM 已接。死亡结算走 `DeathPanel`+`RunTracker`。狼人=可选角色外形（T 变身、判定×1.5、兽化血量/伤害/攻速/移速提升，详见 v1.0.9 文档）。
9. 门禁与资源：EditMode 测试在 `Assets/Tests/EditMode/`（Missing Script 扫描+伤害公式+地牢 seed，提交前跑）；运行时加载资产放 `Assets/Resources/`（路径=编辑器路径镜像，编辑器与构建同源）。误删脚本在**原路径**重建同名类可复活旧 GUID 自愈场景断链（v1.0.4 教训）。
10. **房门防夹人**：`OnTriggerEnter2D` 仅代表刚相交；`RoomTrigger` 必须等玩家世界 `Collider2D.bounds` 完整落入 `Room.Bounds` 后才激活房间关门，固定触发区内缩不能替代完整包围判断。
11. **房内生成门禁**：`DungeonBuilder` 只按最终 `RoomPlan` 绘制一次，Carved 深层格必须真正撤地；Shape 重试须克隆 baseProtect；障碍以完整小岛候选通过格级+玩家口径验证和评分后一次提交，禁止跨尝试污染/增量回滚误删；敌人/奖励/装饰须先从最终 `RoomPlan.SpawnCells`（3×3 无墙可走地面）取点，再做距门与 NonAlloc 物理检测（v1.1.46）。
12. 文档惯例：功能/修复落盘配 `v0.x.y_*.md`；教训回写 `开发必读_核心信息整合.md`；里程碑总账在《项目长期计划_Roguelite路线图》。
