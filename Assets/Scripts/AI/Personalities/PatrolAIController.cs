// PatrolAIController.cs - Patrols waypoints, investigates noise, and engages visible targets.
using UnityEngine;

public class PatrolAIController : AIController
{
    public enum PatrolState { Patrol, Investigate, Chase, Attack }
    [Min(0.1f)] public float investigateWait = 1.5f;
    public PatrolState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private float arrivedAt = -1f;

    protected override void Think()
    {
        if (!HasTargetMemory) { CurrentState = PatrolState.Patrol; arrivedAt = -1f; }
        else if (TargetVisible) { CurrentState = CanFireAtTarget() ? PatrolState.Attack : PatrolState.Chase; arrivedAt = -1f; }
        else if (TargetHeard || CurrentState == PatrolState.Chase || CurrentState == PatrolState.Attack)
        {
            CurrentState = PatrolState.Investigate;
            if (TargetHeard) arrivedAt = -1f;
        }
        switch (CurrentState)
        {
            case PatrolState.Patrol: Patrol(); break;
            case PatrolState.Chase: Chase(); break;
            case PatrolState.Attack: Attack(); break;
            case PatrolState.Investigate:
                if (!Investigate()) arrivedAt = -1f;
                else
                {
                    if (arrivedAt < 0f) arrivedAt = Time.time;
                    if (!TargetHeard && Time.time - arrivedAt >= Mathf.Max(0.1f, investigateWait)) CurrentState = PatrolState.Patrol;
                }
                break;
        }
    }
}
