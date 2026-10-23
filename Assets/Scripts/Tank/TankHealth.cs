// TankHealth.cs - Shared damage and death behavior; never checks the controller type.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TankHealth : MonoBehaviour
{
    [Min(1f)] public float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public bool IsAlive { get { return CurrentHealth > 0f; } }
    public float HealthFraction { get { return CurrentHealth / Mathf.Max(1f, maxHealth); } }
    public event Action<TankHealth> Died;

    private void Awake() { CurrentHealth = Mathf.Max(1f, maxHealth); }

    public void TakeDamage(float amount, TankPawn attacker, bool friendlyFire = false, int? attackerTeam = null)
    {
        if (!IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        TankPawn victim = GetComponent<TankPawn>();
        if (attacker != null && attacker == victim) return;
        int? sourceTeam = attackerTeam ?? (attacker != null ? (int?)attacker.team : null);
        if (!friendlyFire && victim != null && sourceTeam.HasValue && sourceTeam.Value == victim.team) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (CurrentHealth > 0f) return;

        if (victim != null)
        {
            victim.Stop();
            if (victim.OwnerController != null) victim.OwnerController.enabled = false;
        }
        // Deactivation immediately runs OnDisable, removing dead actors from every registry.
        gameObject.SetActive(false);
        if (Died != null) Died(this);
        Destroy(gameObject);
    }
}
