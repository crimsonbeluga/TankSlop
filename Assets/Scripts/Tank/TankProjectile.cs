// TankProjectile.cs - Swept sphere impacts prevent fast shells from skipping thin walls.
using UnityEngine;

[DisallowMultipleComponent]
public class TankProjectile : MonoBehaviour
{
    [Min(0.01f)] public float radius = 0.12f;
    private TankPawn owner;
    private int? ownerTeam;
    private float damage;
    private float speed;
    private float remainingLife;
    private bool friendlyFire;
    private LayerMask hitMask;
    private bool initialized;
    private bool spent;

    private void Awake()
    {
        // Detection uses casts. A visible primitive can retain a collider, but it is disabled.
        foreach (Collider shellCollider in GetComponentsInChildren<Collider>()) shellCollider.enabled = false;
    }

    public void Initialize(TankPawn shooter, float shotDamage, float shotSpeed, float lifetime, bool allowFriendlyFire, LayerMask mask)
    {
        owner = shooter;
        ownerTeam = shooter != null ? (int?)shooter.team : null;
        damage = Mathf.Max(0f, shotDamage);
        speed = Mathf.Max(0f, shotSpeed);
        remainingLife = Mathf.Max(0.01f, lifetime);
        friendlyFire = allowFriendlyFire;
        hitMask = mask;
        initialized = true;
        spent = false;
    }

    private bool CanHit(Collider other)
    {
        if (other == null || other.GetComponentInParent<TankProjectile>() != null) return false;
        return owner == null || other.GetComponentInParent<TankPawn>() != owner;
    }

    private void FixedUpdate()
    {
        if (!initialized || spent) return;
        // Sphere casts do not report starting overlaps, so test those separately.
        Collider[] overlaps = Physics.OverlapSphere(transform.position, Mathf.Max(0.01f, radius), hitMask, QueryTriggerInteraction.Ignore);
        foreach (Collider other in overlaps)
        {
            if (CanHit(other)) { Impact(other); return; }
        }
        float stepTime = Mathf.Min(Time.fixedDeltaTime, remainingLife);
        float distance = speed * stepTime;
        RaycastHit nearest = new RaycastHit();
        bool found = false;
        if (distance > 0f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, Mathf.Max(0.01f, radius), transform.forward, distance, hitMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (CanHit(hit.collider) && (!found || hit.distance < nearest.distance)) { nearest = hit; found = true; }
            }
        }
        if (found)
        {
            transform.position += transform.forward * nearest.distance;
            Impact(nearest.collider);
            return;
        }
        transform.position += transform.forward * distance;
        remainingLife -= stepTime;
        if (remainingLife <= 0f) { spent = true; Destroy(gameObject); }
    }

    private void Impact(Collider other)
    {
        if (spent) return;
        spent = true;
        TankHealth health = other.GetComponentInParent<TankHealth>();
        // Keep the firing team even when the shooter is destroyed before its shell lands.
        if (health != null) health.TakeDamage(damage, owner, friendlyFire, ownerTeam);
        Destroy(gameObject);
    }
}
