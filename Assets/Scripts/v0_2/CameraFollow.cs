using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Header("平滑参数")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private Vector3 currentVelocity;

    void LateUpdate()
    {
        if (target == null) return;

        // 目标位置 = 战士位置 + 偏移（Z轴保持-10，确保相机在2D平面之上）
        Vector3 targetPosition = target.position + offset;

        // 使用SmoothDamp实现平滑跟随，避免生硬抖动
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            1f / smoothSpeed
        );
    }
}
