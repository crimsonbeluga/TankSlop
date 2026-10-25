
using UnityEngine;
using UnityEngine.AI;

// This abstract Controller child provides the common AI actions and conditions. Hunter, Patrol, Coward,
// and Sentry inherit them and override Think() with different finite state machines.
// Each uses the same TankPawn and gameplay components as the player, plus AI-specific TankSenses.
[RequireComponent(typeof(TankPawn), typeof(TankSenses))]
public abstract class AIController : Controller
{
   //Exposed AI Variables / Hearing Condition / Vision Condition:
    // These public settings are inherited by all four personalities and appear in their Inspectors.
    // Designers can tune hearing distance/sensitivity, view distance/FOV, and the narrower in-sights cone.
    [Header("Hearing")]
    [Min(0f)] public float hearingDistance = 18f;
    [Min(0f)] public float hearingSensitivity = 1f;
    [Header("Vision (full cone angles in degrees)")]
    [Min(0f)] public float viewDistance = 25f;
    [Range(0f, 360f)] public float fieldOfView = 110f;
    [Range(0f, 360f)] public float inSightsAngle = 12f;
    public LayerMask perceptionMask = ~0;
    [Header("Combat and memory")]
    [Min(0f)] public float attackRange = 14f;
    [Min(0.1f)] public float memoryDuration = 5f;
    // Exposed AI Variables / Patrol Behavior:
    // Every personality inherits a patrol-point array and loop toggle. Assign waypoint transforms in the scene.
    // loopPatrol chooses whether Patrol wraps to the first waypoint or finishes at the last one.
    [Header("Patrol")]
    public Transform[] patrolPoints = new Transform[0];
    public bool loopPatrol = true;
    [Min(0.1f)] public float waypointTolerance = 0.65f;
    [Header("Steering and obstacle avoidance")]
    [Range(1f, 89f)] public float driveAngle = 55f;
    [Min(0.1f)] public float obstacleLookAhead = 2.2f;
    [Min(0.01f)] public float obstacleProbeRadius = 0.55f;
    public LayerMask obstacleMask = ~0;
  

    // Optional NavMesh paths supply corners to the shared steering code while TankMover still performs movement.
    // This augments the required waypoint patrol; a baked scene NavMesh is needed to demonstrate pathfinding.
    [Header("NavMesh pathfinding (shared by chase, search, patrol and flee)")]
    [Tooltip("Follow baked NavMesh corners using the existing TankMover; no NavMeshAgent component required.")]
    public bool useNavMesh = true;
    [Min(0.1f)] public float navMeshRepathInterval = 0.45f;
    [Min(0.05f)] public float navMeshCornerTolerance = 0.75f;
    [Min(0.1f)] public float navMeshSampleRadius = 2f;
    [Min(0.1f)] public float navMeshDestinationTolerance = 1f;
    // Exposed AI Variables / Flee Behavior / Inversion of Percentage:
    // Designers control the danger distance and minimum/maximum retreat distance used by CalculateFleePoint.
    [Header("Flee inverse percentage")]
    [Min(0.1f)] public float fleeDangerDistance = 18f;
    [Min(0.1f)] public float minimumFleeDistance = 4f;
    [Min(0.1f)] public float maximumFleeDistance = 14f;
    [Header("Debug")]
    public bool drawDebugGizmos = true;

    //Exposed AI Variables:
    // Internal target references, timers, waypoint indices, and route bookkeeping are not serialized Inspector fields.
    // Only tuning data is exposed; runtime state is read through properties where needed.
    protected TankPawn tank;
    protected TankSenses senses;
    private TankPawn target;
    private float lastSensedTime = float.NegativeInfinity;
    private int patrolIndex;
    private bool patrolComplete;
    private float avoidUntil;
    private float avoidDirection = 1f;
    private NavMeshPath navPath;
    private Vector3[] navCorners;
    private Vector3 lastNavDestination;
    private float lastNavRepathTime;
    private float nextNavRepathTime;
    private int navCornerIndex;
    private bool hasNavDestination;
    private bool navStartUnavailable;
    private bool warnedMissingNavMesh;

    public TankPawn Target { get { return target; } }
    public Vector3 LastKnownPosition { get; private set; }
    public bool TargetVisible { get; private set; }
    public bool TargetHeard { get; private set; }
    public bool TargetSensed { get { return TargetVisible || TargetHeard; } }
    public bool HasTargetMemory { get { return IsValidTarget(target) && Time.time - lastSensedTime <= Mathf.Max(0.1f, memoryDuration); } }
    public bool PatrolComplete { get { return patrolComplete; } }
    public abstract string StateName { get; }

    protected override void Awake()
    {
        base.Awake();
        tank = pawn as TankPawn;
        senses = GetComponent<TankSenses>();
    }

    
    // Refresh the shared perception results before dispatching Think() to the concrete personality.
    // The child decides which state/action to use; it reuses this parent's sensing and movement implementations.
    public override void Update()
    {
        base.Update();
        if (tank == null || !tank.IsAlive || senses == null) return;
        UpdateSenses();
        Think();
    }

    protected abstract void Think();

    protected bool IsValidTarget(TankPawn candidate)
    {
        return candidate != null && candidate != tank && candidate.IsAlive && tank != null && candidate.team != tank.team;
    }

   
    public bool CanHear(TankPawn candidate)
    {
        if (!IsValidTarget(candidate) || candidate.Noise == null) return false;
        float noise = candidate.Noise.CurrentNoise;
        float radius = Mathf.Min(Mathf.Max(0f, hearingDistance), noise * Mathf.Max(0f, hearingSensitivity));
        return radius > 0f && (candidate.transform.position - tank.transform.position).sqrMagnitude <= radius * radius;
    }

    //  Vision Condition:
    // A valid target must be inside the view range/FOV AND have an unobstructed line of sight.
    // TankSenses performs the angle and raycast checks; this parent exposes the reusable boolean condition.
    public bool CanSee(TankPawn candidate)
    {
        return IsValidTarget(candidate) && senses != null && senses.IsInFieldOfView(candidate, Mathf.Max(0f, viewDistance), fieldOfView)
            && senses.HasLineOfSight(candidate, perceptionMask);
    }

    // Exposed AI Variables / Vision Condition:
    // Use inSightsAngle and attackRange for the firing cone, separately from the wider detection FOV.
    // Line of sight must also be clear before a target counts as lined up for a shot.
    public bool InSights(TankPawn candidate)
    {
        return IsValidTarget(candidate) && senses != null && senses.IsInFieldOfView(candidate, Mathf.Max(0f, attackRange), inSightsAngle)
            && senses.HasLineOfSight(candidate, perceptionMask);
    }

    //  Shared Conditions / Enemy Shooting:
    // Combine valid target memory, a shooter, the in-sights condition, and the projectile's maximum range.
    // Personalities reuse this condition to choose between approaching and attacking.
    protected bool CanFireAtTarget()
    {
        return HasTargetMemory && tank.Shooter != null && InSights(target)
            && PlanarDistance(tank.transform.position, target.transform.position) <= tank.Shooter.MaximumRange;
    }

    //  Helper Functions 
    // Search GameManager.PlayerControllers, discard invalid candidates, and keep the smallest squared distance.
    // Return the nearest qualifying pawn (or null). sensedOnly requires hearing or sight, so UpdateSenses
    // uses this comparison-based helper without automatically detecting every player in the level.
    public TankPawn FindNearestPlayer(bool sensedOnly = false)
    {
        if (GameManager.Instance == null || tank == null) return null;
        TankPawn nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (PlayerController player in GameManager.Instance.PlayerControllers)
        {
            if (player == null || !player.isActiveAndEnabled) continue;
            TankPawn candidate = player.pawn as TankPawn;
            if (!IsValidTarget(candidate) || (sensedOnly && !CanSee(candidate) && !CanHear(candidate))) continue;
            float distance = (candidate.transform.position - tank.transform.position).sqrMagnitude;
            if (distance < nearestDistance) { nearest = candidate; nearestDistance = distance; }
        }
        return nearest;
    }

    // Helper Functions 
    // Search the automatically maintained AIControllers list for the nearest living same-team ally in range
    // with a valid remembered target. Hunter uses this helper through ReceiveAllyReport.
    public AIController FindNearestAlertAlly(float range)
    {
        if (GameManager.Instance == null || tank == null) return null;
        AIController nearest = null;
        float nearestDistance = Mathf.Max(0f, range) * Mathf.Max(0f, range);
        foreach (AIController ally in GameManager.Instance.AIControllers)
        {
            if (ally == null || ally == this || !ally.isActiveAndEnabled || ally.tank == null || !ally.tank.IsAlive
                || ally.tank.team != tank.team || !ally.HasTargetMemory || !IsValidTarget(ally.Target)) continue;
            float distance = (ally.tank.transform.position - tank.transform.position).sqrMagnitude;
            if (distance <= nearestDistance) { nearest = ally; nearestDistance = distance; }
        }
        return nearest;
    }

    // Hearing Condition / Vision Condition / FSM Integration:
    // Call the shared detection functions and store TargetVisible/TargetHeard for personality transitions.
    // LastKnownPosition and the timestamp preserve limited memory after direct detection is lost.
    private void UpdateSenses()
    {
        TargetVisible = false;
        TargetHeard = false;
        TankPawn detected = FindNearestPlayer(true);
        if (detected != null)
        {
            target = detected;
            TargetVisible = CanSee(target);
            TargetHeard = CanHear(target);
            LastKnownPosition = target.transform.position;
            lastSensedTime = Time.time;
        }
        else if (!HasTargetMemory) target = null;
    }

    //  Up-To-Date List / Game Exceeds Minimum Requirements (candidate feature):
    // Share a nearby ally's remembered target and position. Preserve the original observation time so
    // passing reports between allies does not refresh old information forever.
    protected bool ReceiveAllyReport(float range)
    {
        AIController ally = FindNearestAlertAlly(range);
        if (ally == null) return false;
        target = ally.Target;
        LastKnownPosition = ally.LastKnownPosition;

        lastSensedTime = ally.lastSensedTime;
        return true;
    }

    protected void ForgetTarget()
    {
        target = null;
        lastSensedTime = float.NegativeInfinity;
        TargetVisible = false;
        TargetHeard = false;
    }

    protected void StopTank() { tank.Stop(); }

    //  Chase Behavior / Shared Actions:
    // Subtract positions for the desired ground-plane direction and use SignedAngle for left/right steering.
    // Send bounded turn input through TankPawn so its inherited turnSpeed controls the actual turn rate.
    protected void TurnToward(Vector3 point)
    {
        Vector3 direction = point - tank.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) { tank.HandleRotation(0f); return; }
        float angle = Vector3.SignedAngle(tank.transform.forward, direction, Vector3.up);
        tank.HandleRotation(Mathf.Abs(angle) < 1.5f ? 0f : Mathf.Clamp(angle / 35f, -1f, 1f));
    }

    //  Chase / Flee / Patrol Behaviors:
    // All moving actions share this arrival check and steering entry point. It follows NavMesh corners when
    // available, or local steering when no surface exists, while keeping movement in the shared TankMover.
    protected bool MoveToward(Vector3 point, float stoppingDistance)
    {
        float stop = Mathf.Max(0.05f, stoppingDistance);
        if (PlanarDistance(tank.transform.position, point) <= stop)
        {
            StopTank();
            return true;
        }

        if (useNavMesh)
        {

            NavigationResult navigation = FollowNavMesh(point, stop);
            if (navigation == NavigationResult.Arrived) return true;
            if (navigation != NavigationResult.NoSurface) return false;
        }
        DriveToward(point, true);
        return false;
    }

    private enum NavigationResult { NoSurface, Moving, Arrived, Unreachable }

    private NavigationResult FollowNavMesh(Vector3 destination, float stoppingDistance)
    {
        if (navPath == null) navPath = new NavMeshPath();
        float interval = Mathf.Max(0.1f, navMeshRepathInterval);
        bool goalChanged = hasNavDestination &&
            PlanarDistance(lastNavDestination, destination) >= Mathf.Max(0.1f, navMeshDestinationTolerance);
        bool oldRouteFinished = navCorners != null && navCornerIndex >= navCorners.Length;
        bool canRepathEarly = oldRouteFinished || Time.time - lastNavRepathTime >= Mathf.Min(interval, 0.2f);
        if (!hasNavDestination || Time.time >= nextNavRepathTime || (goalChanged && canRepathEarly))
        {
            hasNavDestination = true;
            lastNavDestination = destination;
            lastNavRepathTime = Time.time;
            nextNavRepathTime = Time.time + interval;
            navCorners = null;
            navCornerIndex = 0;
            navStartUnavailable = false;
            navPath.ClearCorners();

            NavMeshHit originHit;
            if (!NavMesh.SamplePosition(tank.transform.position, out originHit,
                    Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas))
            {
                navStartUnavailable = true;
                if (!warnedMissingNavMesh)
                {
                    Debug.LogWarning("[Tankslop] No NavMesh under " + gameObject.name +
                        ". Bake a NavMeshSurface to navigate walls; using local steering until then.");
                    warnedMissingNavMesh = true;
                }
            }
            else
            {
                NavMeshHit goalHit;
                if (NavMesh.SamplePosition(destination, out goalHit,
                        Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(originHit.position, goalHit.position, NavMesh.AllAreas, navPath) &&
                    navPath.status == NavMeshPathStatus.PathComplete)
                {
                    navCorners = navPath.corners;
                    // Corner 0 is the current location; begin with the first actual turn/goal.
                    navCornerIndex = navCorners != null && navCorners.Length > 1 ? 1 : 0;
                }
            }
        }

        if (navStartUnavailable) return NavigationResult.NoSurface;
        if (navCorners == null || navCorners.Length == 0)
        {
            StopTank();
            return NavigationResult.Unreachable;
        }

        while (navCornerIndex < navCorners.Length &&
            PlanarDistance(tank.transform.position, navCorners[navCornerIndex]) <=
            (navCornerIndex == navCorners.Length - 1 ? stoppingDistance : Mathf.Max(0.1f, navMeshCornerTolerance)))
        {
            navCornerIndex++;
        }
        if (navCornerIndex >= navCorners.Length)
        {
            StopTank();
            return NavigationResult.Arrived;
        }


        DriveToward(navCorners[navCornerIndex], false);
        return NavigationResult.Moving;
    }


    // Chase Behavior / Steering Adjustments:
    // Turn toward the goal, wait to drive if the heading error is too large, and reduce forward input during turns.
    // When local avoidance is requested, compare clearance left/right to choose a turn around an obstacle.
    // All movement is requested through TankPawn.HandleMove and HandleRotation.
    private void DriveToward(Vector3 point, bool useLocalAvoidance)
    {
        Vector3 direction = point - tank.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) { StopTank(); return; }
        if (useLocalAvoidance && Time.time < avoidUntil)
        {
            tank.HandleMove(0f);
            tank.HandleRotation(avoidDirection);
            return;
        }
        float angle = Vector3.SignedAngle(tank.transform.forward, direction, Vector3.up);
        TurnToward(point);
        if (Mathf.Abs(angle) > driveAngle) { tank.HandleMove(0f); return; }
        if (useLocalAvoidance)
        {
            float lookAhead = Mathf.Max(0.1f, obstacleLookAhead);
            if (senses.Clearance(tank.transform.forward, lookAhead, obstacleProbeRadius, obstacleMask) < lookAhead)
            {
                Vector3 left = Quaternion.Euler(0f, -65f, 0f) * tank.transform.forward;
                Vector3 right = Quaternion.Euler(0f, 65f, 0f) * tank.transform.forward;
                float leftClear = senses.Clearance(left, lookAhead, obstacleProbeRadius, obstacleMask);
                float rightClear = senses.Clearance(right, lookAhead, obstacleProbeRadius, obstacleMask);
                avoidDirection = Mathf.Approximately(leftClear, rightClear) ? (angle < 0f ? -1f : 1f) : (rightClear > leftClear ? 1f : -1f);
                avoidUntil = Time.time + 0.45f;
                tank.HandleMove(0f);
                tank.HandleRotation(avoidDirection);
                return;
            }
        }
        tank.HandleMove(Mathf.Clamp01(1f - Mathf.Abs(angle) / 90f));
    }

    // Chase Behavior:
    // The action is implemented once in this abstract parent. Move toward the last sensed player position,
    // or stop and attack when visible and close enough; Hunter and Patrol personalities call this action.
    protected void Chase()
    {
        if (!HasTargetMemory) { StopTank(); return; }
        if (TargetVisible && PlanarDistance(tank.transform.position, LastKnownPosition) <= attackRange * 0.85f) Attack();
        else MoveToward(LastKnownPosition, waypointTolerance);
    }

    //  Enemy Shooting / Pawn/Controller and Component Patterns:
    // Stop driving, turn toward the remembered position, and request a shot only when CanFireAtTarget passes.
    // TankPawn.HandleShoot reaches the same shooter/projectile/damage code used by the player.
    protected void Attack()
    {
        tank.HandleMove(0f);
        if (!HasTargetMemory) { tank.HandleRotation(0f); return; }
        TurnToward(LastKnownPosition);
        if (CanFireAtTarget()) tank.HandleShoot();
    }

    protected bool Investigate() { return MoveToward(LastKnownPosition, waypointTolerance); }

    // Patrol Behavior:
    // Follow the assigned waypoint array using shared MoveToward steering and an arrival tolerance.
    // Increment the index after arrival; wrap to zero if loopPatrol is enabled, otherwise mark the patrol
    // complete and stop. 
    protected void Patrol()
    {
        if (patrolComplete || patrolPoints == null || patrolPoints.Length == 0) { StopTank(); return; }
        for (int attempt = 0; attempt < patrolPoints.Length; attempt++)
        {
            if (patrolIndex >= patrolPoints.Length)
            {
                if (!loopPatrol) { patrolComplete = true; StopTank(); return; }
                patrolIndex = 0;
            }
            Transform point = patrolPoints[patrolIndex];
            if (point == null) { patrolIndex++; continue; }
            if (MoveToward(point.position, waypointTolerance))
            {
                patrolIndex++;
                if (!loopPatrol && patrolIndex >= patrolPoints.Length) patrolComplete = true;
            }
            return;
        }
        StopTank();
    }

    //  Flee Behavior / Inversion of Percentage:
    // Subtract the danger position from the tank position to point away from the player on the ground plane.
    // Normalize that direction, scale it by the computed retreat distance, and add the tank position
    // to get a destination. Flee() then uses shared steering to move toward that point.
    public Vector3 CalculateFleePoint(Vector3 dangerPosition)
    {
        Vector3 away = tank.transform.position - dangerPosition;
        away.y = 0f;
        float distance = away.magnitude;
        if (distance < 0.001f) away = -tank.transform.forward;
        
        float inversePercentage = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, fleeDangerDistance));
        float minimum = Mathf.Max(0.1f, minimumFleeDistance);
        float maximum = Mathf.Max(minimum, maximumFleeDistance);
        float fleeDistance = Mathf.Lerp(minimum, maximum, inversePercentage);
        return tank.transform.position + away.normalized * fleeDistance;
    }

    //  Flee Behavior:
    // Coward's Flee state calls this parent action. Recompute the point opposite the remembered threat
    // and move toward it through the same pathfinding/steering and pawn components used for chasing.
    protected void Flee() { MoveToward(CalculateFleePoint(LastKnownPosition), waypointTolerance); }
    protected float DistanceToLastKnown() { return PlanarDistance(tank.transform.position, LastKnownPosition); }
    protected static float PlanarDistance(Vector3 first, Vector3 second)
    {
        Vector3 difference = first - second; difference.y = 0f; return difference.magnitude;
    }

    // Up-To-Date List:
    // After clearing AI runtime state, call Controller.OnDisable so the AI is removed from both
    // GameManager.Controllers and GameManager.AIControllers.
    protected override void OnDisable()
    {
        TargetVisible = false; TargetHeard = false;
        hasNavDestination = false;
        navCorners = null;
        navCornerIndex = 0;
        base.OnDisable();
    }

    // 
    // Draw hearing range, detection/firing cones, patrol links, and route corners to make tuning visible.
    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, hearingDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward * viewDistance);
        Gizmos.DrawRay(origin, Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward * viewDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, Quaternion.Euler(0f, -inSightsAngle * 0.5f, 0f) * transform.forward * attackRange);
        Gizmos.DrawRay(origin, Quaternion.Euler(0f, inSightsAngle * 0.5f, 0f) * transform.forward * attackRange);
        if (useNavMesh && navCorners != null && navCorners.Length > 0)
        {
            Gizmos.color = Color.cyan;
            int start = navCornerIndex < 0 ? 0 : navCornerIndex;
            if (start < navCorners.Length)
            {
                Gizmos.DrawLine(transform.position + Vector3.up * 0.3f,
                    navCorners[start] + Vector3.up * 0.3f);
                for (int i = start; i < navCorners.Length - 1; i++)
                    Gizmos.DrawLine(navCorners[i] + Vector3.up * 0.3f,
                        navCorners[i + 1] + Vector3.up * 0.3f);
            }
        }
        if (patrolPoints == null) return;
        Gizmos.color = Color.green;
        Transform previous = null;
        Transform first = null;
        foreach (Transform point in patrolPoints)
        {
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, waypointTolerance);
            if (previous != null) Gizmos.DrawLine(previous.position, point.position);
            if (first == null) first = point;
            previous = point;
        }
        if (loopPatrol && previous != null && first != null) Gizmos.DrawLine(previous.position, first.position);
    }
}
