
using UnityEngine;

// 
// Personality 1 of 4. This child defines state choices while inheriting all common actions and conditions.
// Its Idle, Chase, Attack, and Search states differ from the patrol, coward, and sentry state sets.
public class HunterAIController : AIController
{
    // 
    // Think and SetState below define the transitions and Search entry timer.
    public enum HunterState { Idle, Chase, Attack, Search }
    // Exposed AI Variables:
    // Expose ally-report range and search timing for designers; keep the current state and timer internal.
    [Min(0f)] public float allyAlertRange = 18f;
    [Min(0.1f)] public float searchDuration = 2f;
    public HunterState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private float searchStarted;

    protected override void Think()
    {
     
        // If there is no target memory, ReceiveAllyReport searches GameManager.AIControllers for an alert ally.
        // Direct detection was already updated in the parent using GameManager.PlayerControllers.
        bool report = !HasTargetMemory && ReceiveAllyReport(allyAlertRange);
       
        // No memory selects Idle. A sensed target or new report selects Attack when lined up, otherwise Chase.
        // A remembered target without current detection selects Search. The switch executes inherited actions.
        if (!HasTargetMemory) SetState(HunterState.Idle);
        else if (TargetSensed || report) SetState(CanFireAtTarget() ? HunterState.Attack : HunterState.Chase);
        else if (CurrentState != HunterState.Search) SetState(HunterState.Search);

        switch (CurrentState)
        {
            case HunterState.Idle: StopTank(); break;
            case HunterState.Chase: Chase(); break;
            case HunterState.Attack: Attack(); break;
            case HunterState.Search:
                bool arrived = Investigate();
                if (arrived && Time.time - searchStarted >= Mathf.Max(0.1f, searchDuration))
                {
                    ForgetTarget();
                    SetState(HunterState.Idle);
                }
                break;
        }
    }

    //Finite State Machine:
    // Only run the Search entry action when the state changes, so the timer is not restarted every frame.
    // Search may end on memory expiry, or after arrival once searchDuration has elapsed since entering Search.
    private void SetState(HunterState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        if (state == HunterState.Search) searchStarted = Time.time;
    }
}
