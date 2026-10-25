
using UnityEngine;


// Personality 2 of 4. Patrol, Investigate, Chase, and Attack form this child's FSM.
// Route movement, sensing, chasing, and shooting are inherited from the abstract AIController.
public class PatrolAIController : AIController
{
    
    
    public enum PatrolState { Patrol, Investigate, Chase, Attack }
    //Exposed AI Variables:
    // Designers can tune the investigation wait and the inherited patrolPoints and loopPatrol settings.
    [Min(0.1f)] public float investigateWait = 1.5f;
    public PatrolState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private float arrivedAt = -1f;

    //  Patrol Behavior / Chase Behavior / Hearing Condition / Vision Condition:
    // Without target memory, patrol. Visible targets select Attack or Chase; hearing or losing contact
    // from Chase/Attack starts Investigate. These booleans come from the parent's CanHear and CanSee checks.
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
            //  Patrol Behavior:
            // This state calls the parent's waypoint action; loop/stop logic is shared rather than copied into the child.
            // Demonstrate this tank following assigned points through the cube-and-BoxCollider maze.
            case PatrolState.Patrol: Patrol(); break;
            case PatrolState.Chase: Chase(); break;
            case PatrolState.Attack: Attack(); break;
            // Distinct FSM:
            // Move to the remembered position, record arrival, and return to Patrol after investigateWait without hearing.
            // This wait behavior distinguishes the patroller from the Hunter's Search timing and ally-report behavior.
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
