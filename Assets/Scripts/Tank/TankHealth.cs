

using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TankHealth : MonoBehaviour
{
    
    // Player and enemies use this same health component with different data if desired.
    // HealthFraction also supplies the Coward's low-health condition without a separate AI health implementation.
    [Min(1f)] public float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public bool IsAlive { get { return CurrentHealth > 0f; } }
    public float HealthFraction { get { return CurrentHealth / Mathf.Max(1f, maxHealth); } }
    public event Action<TankHealth> Died;

    private void Awake() { CurrentHealth = Mathf.Max(1f, maxHealth); }

   
    // Projectile.Impact calls this method for either side. Reject invalid/self/friendly damage, subtract health,
    // and run the same death path at zero. Different team numbers allow opposing tanks to damage each other.
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
            
        // Deactivate before destroying so Controller and Pawn OnDisable callbacks remove this tank from the registries.
        // The updated controller lists also allow GameManager to detect the round outcome.
        gameObject.SetActive(false);
        if (Died != null) Died(this);
        Destroy(gameObject);
    }
}
