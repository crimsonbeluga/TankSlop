
using UnityEngine;

// Sensing adds functionality for AI; it does not duplicate the shared mover, shooter, or health system.
// AI tuning remains on AIController, which calls these geometry helpers with its chosen values.
[DisallowMultipleComponent]
public class TankSenses : MonoBehaviour
{
    [Tooltip("Optional eye transform. Otherwise uses the center of the hull collider.")]
    public Transform eyes;
    private TankPawn tank;
    private void Awake() { tank = GetComponent<TankPawn>(); }
    public Vector3 EyePosition { get { return eyes != null ? eyes.position : tank.AimPosition; } }

    // Cast along the sight segment and select the nearest blocking hit, ignoring the observer and projectiles.
    // Selecting the nearest hit matters because a wall before the target should block sight.
    // The shooter also reuses this geometry query to check for an obstruction before the muzzle.
    public static bool FirstBlockingHit(TankPawn observer, Vector3 origin, Vector3 direction, float distance, LayerMask mask, out RaycastHit nearest)
    {
        nearest = new RaycastHit();
        if (direction.sqrMagnitude < 0.000001f || distance <= 0f) return false;
        bool found = false;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance, mask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<TankPawn>() == observer
                || hit.collider.GetComponentInParent<TankProjectile>() != null) continue;
            if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
        }
        return found;
    }

    // Draw the sight segment from the eye position to the target's hull center.
    // Sight is clear if nothing blocks the segment or the nearest hit belongs to the target.
    // The perception mask must include wall layers for walls to block this test.
    public bool HasLineOfSight(TankPawn target, LayerMask mask)
    {
        if (tank == null || target == null || !target.IsAlive) return false;
        Vector3 delta = target.AimPosition - EyePosition;
        RaycastHit hit;
        return !FirstBlockingHit(tank, EyePosition, delta, delta.magnitude, mask, out hit)
            || hit.collider.GetComponentInParent<TankPawn>() == target;
    }

    // Check range, then compare the horizontal facing-to-target angle with half the full cone angle.
    // For a 110-degree FOV the target may be up to 55 degrees to either side.
    // AIController.CanSee combines this cone test with HasLineOfSight; neither test alone is enough.
    public bool IsInFieldOfView(TankPawn target, float range, float fullAngle)
    {
        if (tank == null || target == null || !target.IsAlive) return false;
        Vector3 delta = target.AimPosition - EyePosition;
        if (delta.sqrMagnitude > range * range) return false;
        delta.y = 0f;
        Vector3 facing = transform.forward;
        facing.y = 0f;
        return delta.sqrMagnitude < 0.000001f || Vector3.Angle(facing, delta) <= Mathf.Clamp(fullAngle, 0f, 360f) * 0.5f;
    }

    // Sphere-cast in a proposed direction and return the nearest obstruction distance.
    // The parent's DriveToward compares these distances to choose a local steering direction.
    public float Clearance(Vector3 direction, float distance, float radius, LayerMask mask)
    {
        float result = distance;
        RaycastHit[] hits = Physics.SphereCastAll(EyePosition, Mathf.Max(0.01f, radius), direction.normalized, distance, mask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<TankPawn>() == tank
                || hit.collider.GetComponentInParent<TankProjectile>() != null) continue;
            result = Mathf.Min(result, hit.distance);
        }
        return result;
    }
}
