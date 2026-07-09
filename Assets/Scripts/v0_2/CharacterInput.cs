using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(WarriorCharacter))]
public class CharacterInput : MonoBehaviour
{
    private WarriorCharacter character;

    void Awake()
    {
        character = GetComponent<WarriorCharacter>();
    }

    /// <summary>
    /// 由Player Input组件的Unity Event调用。
    /// 接收Input System的Move输入，转发给WarriorCharacter。
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        // 读取Vector2输入（WASD的X和Y）
        Vector2 moveInput = context.ReadValue<Vector2>();
        character.SetMoveInput(moveInput);
    }
}
