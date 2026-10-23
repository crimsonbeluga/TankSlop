// TankPawn.cs - Shared tank facade. Player and AI both use exactly these components.
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TankMover), typeof(TankShooter), typeof(TankHealth))]
[RequireComponent(typeof(TankNoise), typeof(BoxCollider))]
public class TankPawn : Pawn
{
    [Tooltip("0 for the player, 1 for enemies. Matching teams do not damage one another by default.")]
    public int team;

    private TankMover mover;
    private TankShooter shooter;
    private TankHealth health;
    private TankNoise noise;
    private Collider hull;

    // Lazy references avoid depending on the order of component Awake calls.
    public TankMover Mover { get { if (mover == null) mover = GetComponent<TankMover>(); return mover; } }
    public TankShooter Shooter { get { if (shooter == null) shooter = GetComponent<TankShooter>(); return shooter; } }
    public TankHealth Health { get { if (health == null) health = GetComponent<TankHealth>(); return health; } }
    public TankNoise Noise { get { if (noise == null) noise = GetComponent<TankNoise>(); return noise; } }
    public bool IsAlive { get { return isActiveAndEnabled && Health != null && Health.IsAlive; } }
    public Vector3 AimPosition
    {
        get
        {
            if (hull == null) hull = GetComponent<Collider>();
            return hull != null ? hull.bounds.center : transform.position;
        }
    }

    public override void HandleMove(float moveInput)
    {
        if (Mover != null) Mover.Move(IsAlive ? moveInput : 0f);
    }

    public override void HandleRotation(float rotationInput)
    {
        if (Mover != null) Mover.Rotate(IsAlive ? rotationInput : 0f);
    }

    public override bool HandleShoot()
    {
        return IsAlive && Shooter != null && Shooter.TryShoot();
    }

    public override void Stop()
    {
        if (Mover != null) Mover.Stop();
    }
}
