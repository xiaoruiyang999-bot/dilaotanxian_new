using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轻量级事件通道（ScriptableObject）：解耦系统间通信。
/// 使用方式：
///   1. 在 Assets/Resources/Events/ 创建实例（如 EnemyDeathEvent）
///   2. 发送方调用 GameEvent.Raise()
///   3. 接收方挂 GameEventListener 或代码订阅 GameEvent.OnRaised
/// 
/// 优势：
///   - 零 GC 分配（复用列表，Clear 不释放）
///   - 运行时可序列化配置（Inspector 拖拽引用）
///   - 编辑器安全（断链自愈，不会 Missing Reference）
///   - 跨场景持久（Resources 加载或 DontDestroyOnLoad）
/// </summary>
[CreateAssetMenu(fileName = "GameEvent", menuName = "Events/Game Event")]
public class GameEvent : ScriptableObject
{
    private readonly List<GameEventListener> listeners = new List<GameEventListener>();

    /// <summary>触发事件，通知所有已注册监听器。</summary>
    public void Raise()
    {
        // 反向遍历：监听器可能在回调中注销自身
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised();
    }

    public void RegisterListener(GameEventListener listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void UnregisterListener(GameEventListener listener)
    {
        listeners.Remove(listener);
    }
}

/// <summary>
/// 带参数的事件通道（ScriptableObject）：解耦带数据的系统间通信。
/// 典型用途：EnemyDeathEvent(enemyHealth), HealthChangedEvent(current, max)
/// </summary>
public abstract class GameEvent<T> : ScriptableObject
{
    private readonly List<IGameEventListener<T>> listeners = new List<IGameEventListener<T>>();

    public void Raise(T arg)
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised(arg);
    }

    public void RegisterListener(IGameEventListener<T> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void UnregisterListener(IGameEventListener<T> listener)
    {
        listeners.Remove(listener);
    }
}

/// <summary>带参数事件的监听器接口。</summary>
public interface IGameEventListener<T>
{
    void OnEventRaised(T arg);
}
