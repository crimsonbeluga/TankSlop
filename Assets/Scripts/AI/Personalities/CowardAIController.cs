

using UnityEngine;


// Personality 3 of 4. Patrol, Flee, and Counterattack form a different FSM from the other personalities.
// The child chooses when to retreat; the actual Flee action and safe-point calculation live in AIController.
public class CowardAIController : AIController
{
  
    public enum CowardState { Patrol, Flee, Counterattack }
    // Exposed AI Variables:
    // Expose panic/safe distances, the low-health threshold, and retreat/counterattack timing for designers.
    // The inherited flee-distance settings control the retreat point; private timers track this FSM at runtime.
    [Min(0.1f)] public float panicDistance = 9f;
    [Min(0.1f)] public float safeDistance = 12f;
    [Range(0f, 1f)] public float lowHealthFraction = 0.35f;
    [Min(0.1f)] public float counterattackDuration = 2.5f;
    [Min(0.1f)] public float maximumRetreatTime = 3f;
    public CowardState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private float counterattackStarted;
    private float fleeStarted;

    // Flee Behavior / Distinct FSM:
    // A sensed threat triggers Flee when too close or health is low; otherwise the tank counterattacks.
    // Losing valid target memory returns this personality to Patrol.
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
            // Flee Behavior / Inversion of Percentage:
            // Call the inherited Flee action until safe distance or maximum retreat time is reached.
            // That action calculates an opposite-direction destination using an inverse distance percentage, then steers there.
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

    // Finite State Machine:
    // Entering Flee records its start time and stops the previous movement request.
    // BeginCounterattack similarly records its timer and starts the shared Attack action.
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
