using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家瞄准输入层。
/// 负责收集鼠标、手柄或自动瞄准的输入，输出标准化的 AimDirection。
/// 不参与攻击状态、伤害判定或武器 Transform 操作。
/// v0.5 可扩展为支持手柄右摇杆、自动瞄准、网络同步输入。
/// </summary>
public class PlayerAimController : MonoBehaviour
{
    [Header("瞄准来源")]
    [Tooltip("当前使用的瞄准设备类型，预留扩展")]
    [SerializeField] private AimInputDevice inputDevice = AimInputDevice.Mouse;

    [Header("自动瞄准预留")]
    [Tooltip("自动瞄准时搜索最近敌人的半径，0 表示禁用自动瞄准")]
    [SerializeField] private float autoAimRadius = 0f;

    [Tooltip("自动瞄准可命中的目标层")]
    [SerializeField] private LayerMask autoAimLayer;

    /// <summary>
    /// 当前瞄准方向（归一化 Vector2）。
    /// 由 Update 每帧更新，供 WeaponController 读取。
    /// </summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        switch (inputDevice)
        {
            case AimInputDevice.Mouse:
                AimDirection = GetMouseAimDirection();
                break;
            case AimInputDevice.Gamepad:
                // v0.5 扩展：读取手柄右摇杆
                AimDirection = GetGamepadAimDirection();
                break;
            case AimInputDevice.Auto:
                // v0.5 扩展：自动瞄准最近敌人
                AimDirection = GetAutoAimDirection();
                break;
            default:
                AimDirection = Vector2.right;
                break;
        }

        if (AimDirection.sqrMagnitude < 0.0001f)
        {
            AimDirection = Vector2.right;
        }
        else
        {
            AimDirection.Normalize();
        }
    }

    private Vector2 GetMouseAimDirection()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Vector2 mouseScreen = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;
        Vector2 mouseWorld = mainCamera != null
            ? (Vector2)mainCamera.ScreenToWorldPoint(mouseScreen)
            : mouseScreen;

        return (mouseWorld - (Vector2)transform.position).normalized;
    }

    private Vector2 GetGamepadAimDirection()
    {
        // v0.5 实现手柄右摇杆输入
        return AimDirection;
    }

    private Vector2 GetAutoAimDirection()
    {
        if (autoAimRadius <= 0f)
        {
            return AimDirection;
        }

        // v0.5 实现最近敌人搜索
        return AimDirection;
    }
}

/// <summary>
/// 瞄准输入设备类型，供 PlayerAimController 扩展使用。
/// </summary>
public enum AimInputDevice
{
    Mouse,
    Gamepad,
    Auto
}
