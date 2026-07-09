using UnityEngine;

public interface IAttack
{
    float AttackRange { get; }
    float AttackDamage { get; }
    void Execute();
}
