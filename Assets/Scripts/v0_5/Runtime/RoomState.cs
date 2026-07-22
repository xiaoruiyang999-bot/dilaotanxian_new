/// <summary>
/// 房间状态机状态（计划书五-B）：Unvisited → Active → Cleared，不可返回。
/// </summary>
public enum RoomState { Unvisited, Active, Cleared }

/// <summary>
/// 清房条件。None = 无战斗直接完成（门常开）；AllEnemiesDead = 注册敌人全灭才完成。
/// v0.5.3 起每种类型的条件由 RoomTypeConfig 数据驱动。
/// </summary>
public enum RoomClearCondition { None, AllEnemiesDead }
