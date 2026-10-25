

using UnityEngine;


// Locomotion lives in this reusable component, separate from player input and AI decision making.
// Both controller types reach it through TankPawn; it uses pawn data rather than checking player versus AI.
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
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

   
    // Move and Rotate cache bounded input requests. FixedUpdate performs the Rigidbody changes
    // at the physics timestep rather than moving the transform inside the controller.
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

  
    // Test the proposed hull pose for overlap so movement and turning can be limited by nearby walls.
    // Demonstrate the collision behavior in the test environment; this check alone does not prove it is bug-free.
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
       
        // Turn angle = rotation input * turnSpeed * fixedDeltaTime. Compose a yaw quaternion with the current rotation.
        // Forward displacement = rotated forward direction * move input * moveSpeed * fixedDeltaTime.
        Quaternion oldRotation = body.rotation;
        Quaternion rotation = oldRotation * Quaternion.Euler(0f, rotationInput * Mathf.Max(0f, tank.turnSpeed) * Time.fixedDeltaTime, 0f);
        if (!CanOccupy(body.position, rotation)) rotation = oldRotation;

        Vector3 displacement = rotation * Vector3.forward * (moveInput * Mathf.Max(0f, tank.moveSpeed) * Time.fixedDeltaTime);
        float distance = displacement.magnitude;
        if (distance > 0f)
        {

            RaycastHit[] hits = body.SweepTestAll(displacement / distance, distance + collisionSkin, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (BlocksMovement(hit.collider) && Mathf.Abs(hit.normal.y) < 0.7f)
                    distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - collisionSkin));
            }
            if (!CanOccupy(body.position + displacement.normalized * distance, rotation))
            {
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
           
            // MovePosition applies the allowed displacement to the Rigidbody after collision checks.
            body.MovePosition(body.position + displacement.normalized * distance);
        }
     
        // Rotate(input) is this mover's command method; Rigidbody.MoveRotation applies the quaternion here.
        
        body.MoveRotation(rotation);
     
        // Report accepted movement or turning to TankNoise. Blocked translation contributes no movement noise;
        // successful turning can still make noise. TankNoise exposes the noise amount for designers.
        float actualMove = distance > 0.0001f ? Mathf.Abs(moveInput) : 0f;
        float actualTurn = Quaternion.Angle(oldRotation, rotation) > 0.01f ? Mathf.Abs(rotationInput) : 0f;
        if (tank.Noise != null) tank.Noise.SetMovement(Mathf.Max(actualMove, actualTurn));
    }

    private void OnDisable() { Stop(); }
}
