
using UnityEngine;


// Required components give this pawn movement, shooting, health, noise, and a hull collider.
// Use TankPawn on both player and enemy objects
[DisallowMultipleComponent]
[RequireComponent(typeof(TankMover), typeof(TankShooter), typeof(TankHealth))]
[RequireComponent(typeof(TankNoise), typeof(BoxCollider))]

// TankPawn inherits Pawn's speed settings, ownership, and registration, then implements tank-specific commands.
// The same foundation continues to serve the player when AI is added for Milestone 2.
public class TankPawn : Pawn
{
    
    // Team is configurable data on the shared pawn. Damage filtering compares team values,
    // so the health component does not need separate player and enemy implementations.
    [Tooltip("0 for the player, 1 for enemies. Matching teams do not damage one another by default.")]
    public int team;

    private TankMover mover;
    private TankShooter shooter;
    private TankHealth health;
    private TankNoise noise;
    private Collider hull;

   
    // Mover, Shooter, Health, and Noise expose the common tank components through TankPawn.
    // Controllers can access these systems through their pawn. AI-only sensing is obtained by AIController.
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

   
    // These overrides forward movement and rotation to the same TankMover for players and AI.
    // The pawn checks whether it is alive, without branching on which controller type owns it.
    public override void HandleMove(float moveInput)
    {
        if (Mover != null) Mover.Move(IsAlive ? moveInput : 0f);
    }

    public override void HandleRotation(float rotationInput)
    {
        if (Mover != null) Mover.Rotate(IsAlive ? rotationInput : 0f);
    }

    
    // Both controller types call this entry point, which uses the same TankShooter.TryShoot implementation.
    public override bool HandleShoot()
    {
        return IsAlive && Shooter != null && Shooter.TryShoot();
    }

    public override void Stop()
    {
        if (Mover != null) Mover.Stop();
    }
}
