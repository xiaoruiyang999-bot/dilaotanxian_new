using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private float attackCooldown = 0.5f;

    private float cooldownTimer = 0f;
    private bool canAttack = true;
    private IAttack currentAttack;

    public System.Action OnAttackStart;
    public System.Action OnAttackEnd;

    void Awake()
    {
        currentAttack = GetComponentInChildren<IAttack>();
        if (currentAttack == null)
            Debug.LogWarning("[PlayerCombat] 未找到IAttack实现！");
    }

    void Update()
    {
        if (!canAttack)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f) canAttack = true;
        }
    }

    public void TryAttack()
    {
        if (!canAttack) return;
        if (currentAttack == null) return;

        OnAttackStart?.Invoke();
        currentAttack.Execute();
        OnAttackEnd?.Invoke();

        canAttack = false;
        cooldownTimer = attackCooldown;
    }

    public void SetAttack(IAttack newAttack) => currentAttack = newAttack;
}
