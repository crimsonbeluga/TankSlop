// TankSenses.cs - Physics queries used by AI; angles and sensitivity live on AIController.
using UnityEngine;

[DisallowMultipleComponent]
public class TankSenses : MonoBehaviour
{
    [Tooltip("Optional eye transform. Otherwise uses the center of the hull collider.")]
    public Transform eyes;
    private TankPawn tank;
    private void Awake() { tank = GetComponent<TankPawn>(); }
    public Vector3 EyePosition { get { return eyes != null ? eyes.position : tank.AimPosition; } }

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

    public bool HasLineOfSight(TankPawn target, LayerMask mask)
    {
        if (tank == null || target == null || !target.IsAlive) return false;
        Vector3 delta = target.AimPosition - EyePosition;
        RaycastHit hit;
        return !FirstBlockingHit(tank, EyePosition, delta, delta.magnitude, mask, out hit)
            || hit.collider.GetComponentInParent<TankPawn>() == target;
    }

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
