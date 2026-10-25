

using UnityEngine;

[DisallowMultipleComponent]
public class TankShooter : MonoBehaviour
{
    
    // Configure muzzle, projectile prefab, damage, speed, lifetime, and cooldown on the same component for both sides.
    public Transform firePoint;
    public TankProjectile projectilePrefab;
    [Min(0.01f)] public float shotCooldown = 0.8f;
    [Min(0f)] public float damage = 25f;
    [Min(0.01f)] public float projectileSpeed = 18f;
    [Min(0.01f)] public float projectileLifetime = 4f;
    public bool friendlyFire;
    public LayerMask hitMask = ~0;
    private float nextShotTime;
    private TankPawn tank;
    public float MaximumRange { get { return Mathf.Max(0f, projectileSpeed) * Mathf.Max(0f, projectileLifetime); } }

    private void Awake() { tank = GetComponent<TankPawn>(); }

    // TankPawn.HandleShoot reaches this method from either controller. Reject dead tanks, cooldown, or missing setup,
    // then create a shell with the firing tank and its damage settings. No separate AI shooting code is needed.
    public bool TryShoot()
    {
        if (tank == null || !tank.IsAlive || Time.time < nextShotTime || projectilePrefab == null || firePoint == null) return false;
        Vector3 spawnPosition = firePoint.position;
        Vector3 toMuzzle = spawnPosition - tank.AimPosition;
        RaycastHit obstruction;
        if (TankSenses.FirstBlockingHit(tank, tank.AimPosition, toMuzzle, toMuzzle.magnitude, hitMask, out obstruction))
        {
            spawnPosition = obstruction.point - toMuzzle.normalized * (projectilePrefab.radius + 0.01f);
        }
        TankProjectile shell = Instantiate(projectilePrefab, spawnPosition, firePoint.rotation);
        shell.Initialize(tank, damage, projectileSpeed, projectileLifetime, friendlyFire, hitMask);
        nextShotTime = Time.time + Mathf.Max(0.01f, shotCooldown);
        // Hearing Condition:
        // A successfully spawned shot starts the pawn's shooting-noise pulse, whose amount is exposed in TankNoise.
        if (tank.Noise != null) tank.Noise.MakeShotNoise();
        return true;
    }
}
