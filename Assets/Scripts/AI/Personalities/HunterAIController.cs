// HunterAIController.cs - Pursues sensed or ally-reported targets, then searches last sighting.
using UnityEngine;

public class HunterAIController : AIController
{
    public enum HunterState { Idle, Chase, Attack, Search }
    [Min(0f)] public float allyAlertRange = 18f;
    [Min(0.1f)] public float searchDuration = 2f;
    public HunterState CurrentState { get; private set; }
    public override string StateName { get { return CurrentState.ToString(); } }
    private float searchStarted;

    protected override void Think()
    {
        bool report = !HasTargetMemory && ReceiveAllyReport(allyAlertRange);
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

    private void SetState(HunterState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        if (state == HunterState.Search) searchStarted = Time.time;
    }
}
