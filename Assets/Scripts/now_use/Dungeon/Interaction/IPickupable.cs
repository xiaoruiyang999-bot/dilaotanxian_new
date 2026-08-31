using UnityEngine;

/// <summary>
/// 可拾取物接口（v0.6.1 两段式拾取框架，计划书 4.3）。
/// 武器/道具/宠物等可拾取物实现本接口，由 PlayerInteractor 探测、列表选择并结算拾取。
/// 与普通 Interactable（宝箱/祭坛等按 E 直接触发）区分：可拾取物支持多物品列表选择。
/// </summary>
public interface IPickupable
{
    /// <summary>拾取列表中的显示名（如 "治疗球 +2HP"）。</summary>
    string DisplayName { get; }

    /// <summary>拾取结算（应用效果并自行销毁/隐藏）。player 为拾取者。</summary>
    void OnPickedUp(GameObject player);
}
