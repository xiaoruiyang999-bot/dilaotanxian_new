# now_use — 当前版本实际在用脚本（v0.4.6）

> 本文件夹**只存放当前版本（场景 `v0_4_EnemySystem.unity` + Player/Enemy/TrainingDummy）实际运行所需的脚本**。
> 历史/弃用脚本保留在原版本文件夹（`v0_2` / `v0_3` / `v0_4` / `Framework`）作为档案，不删除。
>
> **维护约定（每个新版本必须执行）**：
> 1. 新版本中新增并在用的脚本 → 移入本文件夹（连带 .meta，GUID 不变，引用不断）。
> 2. 新版本中弃用的脚本 → 移出其所属版本文件夹归档。
> 3. 每次变动后更新本 README 的清单与版本号。
> 4. 移动用 Unity 资产移动（Editor 拖拽或 asset_move），保证 .meta 跟随，不要在外部资源管理器裸移。

## 清单（23 个，按职责分组）

### Player 侧
| 脚本 | 职责 |
|------|------|
| PlayerController | 输入接收、移动、死亡处理，组件门面 |
| PlayerStats | HP/护甲上限、护甲脱战恢复、护甲吸收伤害 |
| Health | 玩家生命值（IDamageable），事件通知 |
| PlayerCombat | 攻击三阶段状态机，Active 驱动 WeaponHitbox |
| PlayerAimController | 鼠标瞄准方向输入层 |
| PlayerUI | 屏幕左下角固定 HP/护甲条 |
| PlayerWorldStatusBar | 头顶世界空间状态条（蓝护甲 + 红 HP） |

### 武器 / 攻击框架（Player/Enemy 共用）
| 脚本 | 职责 |
|------|------|
| AttackData | 攻击配置 SO（时长/范围/角度/伤害/冷却/目标层/动画），唯一数据源 |
| WeaponController | WeaponPivot 朝向、WeaponSprite 视觉、攻击方向锁定 |
| WeaponAnimator | DOTween 挥动动画（纯视觉） |
| WeaponHitbox | Active 阶段武器矩形命中检测与伤害结算 |
| AttackIndicator | 扇形/圆形预警 Mesh（纯视觉） |
| AttackQuery | 瞬时范围查询工具（v0.4.6 起近战不再调用，挂在 prefab 上留待 v0.5 技能） |

### Enemy 侧
| 脚本 | 职责 |
|------|------|
| EnemyController | 移动/朝向门面、受伤闪烁、死亡处理 |
| EnemyStats | 敌人属性（部分攻击字段已迁移 AttackData） |
| EnemyHealth | 敌人生命值（IDamageable），受击通知 AI |
| EnemyCombat | 敌人攻击状态机 + 冷却 |
| EnemyAI | Patrol/Chase/Attack/ReturnToPatrol 状态机 |
| TrainingDummy | 伤害测试木桩（场景中在用） |

### 通用
| 脚本 | 职责 |
|------|------|
| WorldSpaceHealthBar | 敌人头顶血条 |
| CameraFollow | 相机平滑跟随 |
| PatrolSystem | 巡逻点生成（EnemyAI 使用） |
| IDamageable | 统一伤害接口 |

## 明确不在用（留在档案目录）
- `v0_2/CharacterInput.cs`、`v0_2/WarriorCharacter.cs` — v0.2 旧场景专用
- `v0_3/Enemy.cs`、`v0_3/WarriorAttack.cs`、`v0_3/Interfaces/IAttack.cs` — 已弃用
- `Framework/DamageSystem.cs`、`Framework/DetectionSystem.cs` — 未被任何脚本引用
