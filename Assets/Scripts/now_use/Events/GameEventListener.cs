using UnityEngine;

/// <summary>
/// Unity 组件监听器：挂载到 GameObject 上，Inspector 拖拽事件通道和响应方法。
/// 适用于需要可视化配置的场景（如 UI 面板监听游戏事件）。
/// </summary>
public class GameEventListener : MonoBehaviour
{
    [Tooltip("要监听的事件通道（ScriptableObject）")]
    [SerializeField] private GameEvent gameEvent;

    [Tooltip("事件触发时调用的 UnityEvent")]
    [SerializeField] private UnityEngine.Events.UnityEvent response;

    private void OnEnable()
    {
        if (gameEvent != null)
            gameEvent.RegisterListener(this);
    }

    private void OnDisable()
    {
        if (gameEvent != null)
            gameEvent.UnregisterListener(this);
    }

    public void OnEventRaised()
    {
        response?.Invoke();
    }
}

/// <summary>
/// 带参数的 Unity 组件监听器：Inspector 拖拽事件通道和响应方法。
/// </summary>
public class GameEventListener<T> : MonoBehaviour, IGameEventListener<T>
{
    [Tooltip("要监听的事件通道（ScriptableObject）")]
    [SerializeField] private GameEvent<T> gameEvent;

    private readonly System.Collections.Generic.List<System.Action<T>> responses = new System.Collections.Generic.List<System.Action<T>>();

    public void AddResponse(System.Action<T> response)
    {
        if (!responses.Contains(response))
            responses.Add(response);
    }

    public void RemoveResponse(System.Action<T> response)
    {
        responses.Remove(response);
    }

    private void OnEnable()
    {
        if (gameEvent != null)
            gameEvent.RegisterListener(this);
    }

    private void OnDisable()
    {
        if (gameEvent != null)
            gameEvent.UnregisterListener(this);
    }

    public void OnEventRaised(T arg)
    {
        for (int i = responses.Count - 1; i >= 0; i--)
            responses[i]?.Invoke(arg);
    }
}


