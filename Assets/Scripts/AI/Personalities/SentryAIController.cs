

using UnityEngine;

// Personality 4 of 4. Guard, Scan, Attack, and ReturnHome create a stationary guard-style FSM.
// It inherits the same sensing, steering, and attack actions used by the moving personalities.
public class SentryAIController : AIController
{
   
    // Draw visibility-to-Attack, hearing/lost-sight-to-Scan, scan timeout-to-ReturnHome, and arrival-to-Guard.
    // The code defines the behavior; a separate drawn FSM must still be included in the project.
    public enum SentryState { Guard, Scan, Attack, ReturnHome }
  
    // Expose scan duration while keeping home pose and timer bookkeeping internal.
    // Hearing, view cone, in-sights cone, and patrol settings are inherited from AIController.
    [Min(0.1f)] public float scanDuration = 3f;
    public SentryState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private Vector3 homePosition;
    private Vector3 homeForward;
    private float scanStarted;

    
    // Call the parent setup first, then capture this sentry's home position and facing as its own initialization.
    protected override void Awake()
    {
        base.Awake();
        homePosition = tank != null ? tank.transform.position : transform.position;
        homeForward = tank != null ? tank.transform.forward : transform.forward;
    }

    
    // Seeing a target selects Attack. Hearing a target or losing sight from Attack enters Scan.
    // After the scan timeout without continued hearing, return home and resume the original guard heading.
    protected override void Think()
    {
        if (TargetVisible) CurrentState = SentryState.Attack;
        else if ((TargetHeard || CurrentState == SentryState.Attack) && CurrentState != SentryState.Scan)
        {
            CurrentState = SentryState.Scan;
            scanStarted = Time.time;
        }
        switch (CurrentState)
        {
            case SentryState.Guard:
                tank.HandleMove(0f);
                TurnToward(homePosition + homeForward * 10f);
                break;
            case SentryState.Scan:
                tank.HandleMove(0f);
                if (HasTargetMemory) TurnToward(LastKnownPosition);
                else tank.HandleRotation(0f);
                if (!TargetHeard && Time.time - scanStarted >= Mathf.Max(0.1f, scanDuration)) CurrentState = SentryState.ReturnHome;
                break;
            // Enemy Shooting / Shared Actions:
            // Reuse the parent's Attack, which turns and calls TankPawn.HandleShoot when its firing conditions pass.
            case SentryState.Attack: Attack(); break;
            case SentryState.ReturnHome:
                if (MoveToward(homePosition, waypointTolerance)) CurrentState = SentryState.Guard;
                break;
        }
    }
}
