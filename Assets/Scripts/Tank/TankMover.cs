// TankMover.cs - Tank locomotion at the physics timestep, with swept wall checks.
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class TankMover : MonoBehaviour
{
    [Min(0.001f)] public float collisionSkin = 0.03f;
    public LayerMask collisionMask = ~0;
    private Rigidbody body;
    private BoxCollider hull;
    private TankPawn tank;
    private float moveInput;
    private float rotationInput;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        hull = GetComponent<BoxCollider>();
        tank = GetComponent<TankPawn>();
        // A flat X/Z arena uses controlled motion rather than gravity or physical tipping.
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void Move(float input) { moveInput = Mathf.Clamp(input, -1f, 1f); }
    public void Rotate(float input) { rotationInput = Mathf.Clamp(input, -1f, 1f); }
    public void Stop()
    {
        moveInput = 0f;
        rotationInput = 0f;
        if (tank != null && tank.Noise != null) tank.Noise.SetMovement(0f);
    }

    private bool BlocksMovement(Collider other)
    {
        return other != null && !other.transform.IsChildOf(transform) && other.GetComponentInParent<TankProjectile>() == null
            && (collisionMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private bool CanOccupy(Vector3 position, Quaternion rotation)
    {
        if (hull == null) return true;
        Vector3 scale = transform.lossyScale;
        Vector3 half = Vector3.Scale(hull.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.5f;
        half = new Vector3(Mathf.Max(0.01f, half.x - collisionSkin), Mathf.Max(0.01f, half.y - collisionSkin), Mathf.Max(0.01f, half.z - collisionSkin));
        Vector3 center = position + rotation * Vector3.Scale(hull.center, scale);
        Collider[] overlaps = Physics.OverlapBox(center, half, rotation, collisionMask, QueryTriggerInteraction.Ignore);
        foreach (Collider other in overlaps) if (BlocksMovement(other)) return false;
        return true;
    }

    private void FixedUpdate()
    {
        if (tank == null || !tank.IsAlive) { Stop(); return; }
        Quaternion oldRotation = body.rotation;
        Quaternion rotation = oldRotation * Quaternion.Euler(0f, rotationInput * Mathf.Max(0f, tank.turnSpeed) * Time.fixedDeltaTime, 0f);
        if (!CanOccupy(body.position, rotation)) rotation = oldRotation;

        Vector3 displacement = rotation * Vector3.forward * (moveInput * Mathf.Max(0f, tank.moveSpeed) * Time.fixedDeltaTime);
        float distance = displacement.magnitude;
        if (distance > 0f)
        {
            // Sweeps prevent kinematic MovePosition from driving through static maze walls.
            // Use the current hull orientation for the sweep; rotating corners are checked above.
            RaycastHit[] hits = body.SweepTestAll(displacement / distance, distance + collisionSkin, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                // Horizontal locomotion should not be stopped by a tangent floor contact.
                if (BlocksMovement(hit.collider) && Mathf.Abs(hit.normal.y) < 0.7f)
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - collisionSkin));
            }
            if (!CanOccupy(body.position + displacement.normalized * distance, rotation))
            {
                // Recheck the destination using the new orientation, including corner rotation.
                float clear = 0f;
                float blocked = distance;
                for (int step = 0; step < 7; step++)
                {
                    float candidate = (clear + blocked) * 0.5f;
                    if (CanOccupy(body.position + displacement.normalized * candidate, rotation)) clear = candidate;
                    else blocked = candidate;
                }
                distance = clear;
            }
            body.MovePosition(body.position + displacement.normalized * distance);
        }
        body.MoveRotation(rotation);
        float actualMove = distance > 0.0001f ? Mathf.Abs(moveInput) : 0f;
        float actualTurn = Quaternion.Angle(oldRotation, rotation) > 0.01f ? Mathf.Abs(rotationInput) : 0f;
        if (tank.Noise != null) tank.Noise.SetMovement(Mathf.Max(actualMove, actualTurn));
    }

    private void OnDisable() { Stop(); }
}
