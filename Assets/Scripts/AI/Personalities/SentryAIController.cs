// SentryAIController.cs - Defends its starting post, scans toward sounds, and never chases.
using UnityEngine;

public class SentryAIController : AIController
{
    public enum SentryState { Guard, Scan, Attack, ReturnHome }
    [Min(0.1f)] public float scanDuration = 3f;
    public SentryState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private Vector3 homePosition;
    private Vector3 homeForward;
    private float scanStarted;

    protected override void Awake()
    {
        base.Awake();
        homePosition = tank != null ? tank.transform.position : transform.position;
        homeForward = tank != null ? tank.transform.forward : transform.forward;
    }

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
            case SentryState.Attack: Attack(); break;
            case SentryState.ReturnHome:
                if (MoveToward(homePosition, waypointTolerance)) CurrentState = SentryState.Guard;
                break;
        }
    }
}
