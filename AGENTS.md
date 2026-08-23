# AGENTS.md — 本项目开发指令（ZCode 每次会话自动加载）

2D 地牢 Roguelite（Unity 6 + URP 2D + Input System + DOTween），脚本在 `Assets/Scripts/now_use/`（v0_1~v0_4 是归档、Framework/ 是死代码，禁止引用）。

**写代码或改资产的任务开始时，必须先调用 `project-dev-rules` skill 加载完整红线**（本文件只是摘要层；skill 是全量层；根目录《开发必读_核心信息整合.md》是人读真源，三者更新需同步）。最高优先级规则摘要：

1. **同源原则**：预警=判定=武器视觉消费同一份 AttackData；判定几何唯一真源是 `WeaponHitbox.CurrentBoxGeometry`。
2. **Trigger 只参与逻辑事件**：LOS 射线/投射物/移动探测一律跳过 isTrigger；实体碰撞（墙）才阻挡。
3. **Prefab 实例上 component_add 会被 scene_load 丢弃**——组件要进 Player.prefab 本体（prefab_apply_overrides 路径）。
4. **YAML 手术**（场景 LF / Player.prefab CRLF）：备份→行尾感知→fileID 查重→组件三处双向（块+GO列表+父TF children）→SceneRoots 同步→scene_load 重载验证。UnitySkills 设图片导入必须显式 `spriteMode=Single`（默认 Multiple 自动切片=透明碎片）。
5. **物理查询零 GC**：NonAlloc+静态缓冲；协程互斥+基线色缓存；玩家对象从不销毁（死亡走 Respawn，新增状态字段必须在其内重置）。
6. **720px 序列帧默认 7.2 世界单位**，需 scale/PPU 校正；`#if UNITY_EDITOR` 的 AssetDatabase 便利代码打包前要资源化。
7. **外部改场景后必须 Reload 再 Play**；调试遵守《开发调试规范》（禁动相机/瞬移玩家/留 Debug_ 对象）。
8. 键位：WASD 移动 / 左键攻击 / Space=Dash（无敌帧）/ **T=狼人变身**。反馈挂点：命中三件套在 WeaponHitbox，音效走 `AudioManager.PlaySFX(id)`（未配置静默）。
9. 文档惯例：功能/修复落盘配 `v0.x.y_*.md`；教训回写 `开发必读_核心信息整合.md`；里程碑总账在《项目长期计划_Roguelite路线图》。
