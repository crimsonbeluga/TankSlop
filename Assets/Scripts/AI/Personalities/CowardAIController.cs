 // CowardAIController.cs - Withdraws on contact, then briefly turns back to counterattack.
using UnityEngine;

public class CowardAIController : AIController
{
    public enum CowardState { Patrol, Flee, Counterattack }
    [Min(0.1f)] public float panicDistance = 9f;
    [Min(0.1f)] public float safeDistance = 12f;
    [Range(0f, 1f)] public float lowHealthFraction = 0.35f;
    [Min(0.1f)] public float counterattackDuration = 2.5f;
    [Min(0.1f)] public float maximumRetreatTime = 3f;
    public CowardState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private float counterattackStarted;
    private float fleeStarted;

    protected override void Think()
    {
        if (!HasTargetMemory) CurrentState = CowardState.Patrol;
        switch (CurrentState)
        {
            case CowardState.Patrol:
                if (TargetSensed)
                {
                    if (DistanceToLastKnown() <= panicDistance || tank.Health.HealthFraction <= lowHealthFraction) BeginFlee();
                    else BeginCounterattack();
                }
                else Patrol();
                break;
            case CowardState.Flee:
                if (DistanceToLastKnown() >= Mathf.Max(panicDistance + 1f, safeDistance)
                    || Time.time - fleeStarted >= Mathf.Max(0.1f, maximumRetreatTime)) BeginCounterattack();
                else Flee();
                break;
            case CowardState.Counterattack:
                Attack();
                if (Time.time - counterattackStarted >= Mathf.Max(0.1f, counterattackDuration))
                {
                    if (TargetSensed) BeginFlee();
                    else CurrentState = CowardState.Patrol;
                }
                break;
        }
    }

    private void BeginFlee()
    {
        CurrentState = CowardState.Flee;
        fleeStarted = Time.time;
        StopTank();
    }

    private void BeginCounterattack()
    {
        CurrentState = CowardState.Counterattack;
        counterattackStarted = Time.time;
        Attack();
    }
}
