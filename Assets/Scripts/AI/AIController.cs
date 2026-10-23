// AIController.cs - Shared actions, conditions, sensing, and search helpers for four FSMs.
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(TankPawn), typeof(TankSenses))]
public abstract class AIController : Controller
{
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
    [Header("Patrol")]
    public Transform[] patrolPoints = new Transform[0];
    public bool loopPatrol = true;
    [Min(0.1f)] public float waypointTolerance = 0.65f;
    [Header("Steering and obstacle avoidance")]
    [Range(1f, 89f)] public float driveAngle = 55f;
    [Min(0.1f)] public float obstacleLookAhead = 2.2f;
    [Min(0.01f)] public float obstacleProbeRadius = 0.55f;
    public LayerMask obstacleMask = ~0;
    [Header("NavMesh pathfinding (shared by chase, search, patrol and flee)")]
    [Tooltip("Follow baked NavMesh corners using the existing TankMover; no NavMeshAgent component required.")]
    public bool useNavMesh = true;
    [Min(0.1f)] public float navMeshRepathInterval = 0.45f;
    [Min(0.05f)] public float navMeshCornerTolerance = 0.75f;
    [Min(0.1f)] public float navMeshSampleRadius = 2f;
    [Min(0.1f)] public float navMeshDestinationTolerance = 1f;
    [Header("Flee inverse percentage")]
    [Min(0.1f)] public float fleeDangerDistance = 18f;
    [Min(0.1f)] public float minimumFleeDistance = 4f;
    [Min(0.1f)] public float maximumFleeDistance = 14f;
    [Header("Debug")]
    public bool drawDebugGizmos = true;

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

    public bool CanSee(TankPawn candidate)
    {
        return IsValidTarget(candidate) && senses != null && senses.IsInFieldOfView(candidate, Mathf.Max(0f, viewDistance), fieldOfView)
            && senses.HasLineOfSight(candidate, perceptionMask);
    }

    public bool InSights(TankPawn candidate)
    {
        return IsValidTarget(candidate) && senses != null && senses.IsInFieldOfView(candidate, Mathf.Max(0f, attackRange), inSightsAngle)
            && senses.HasLineOfSight(candidate, perceptionMask);
    }

    protected bool CanFireAtTarget()
    {
        return HasTargetMemory && tank.Shooter != null && InSights(target)
            && PlanarDistance(tank.transform.position, target.transform.position) <= tank.Shooter.MaximumRange;
    }

    // Linear nearest-object searches use the live singleton lists rather than scene searches.
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

    protected bool ReceiveAllyReport(float range)
    {
        AIController ally = FindNearestAlertAlly(range);
        if (ally == null) return false;
        target = ally.Target;
        LastKnownPosition = ally.LastKnownPosition;
        // Copy the observation time as well: allies cannot refresh a forgotten target forever.
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

    protected void TurnToward(Vector3 point)
    {
        Vector3 direction = point - tank.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) { tank.HandleRotation(0f); return; }
        float angle = Vector3.SignedAngle(tank.transform.forward, direction, Vector3.up);
        tank.HandleRotation(Mathf.Abs(angle) < 1.5f ? 0f : Mathf.Clamp(angle / 35f, -1f, 1f));
    }

    // All moving FSM actions use this function. Only TankMover applies position/rotation.
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
            // A missing baked surface uses the previous local steering so old scenes stay playable.
            // A real NavMesh with no COMPLETE route must not drive directly into a wall.
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
        // Periodic refresh also handles moving goals. If an FSM changes destination
        // (e.g. a patrol reaches a waypoint), do not let it follow the previous route.
        bool goalChanged = hasNavDestination &&
            PlanarDistance(lastNavDestination, destination) >= Mathf.Max(0.1f, navMeshDestinationTolerance);
        bool oldRouteFinished = navCorners != null && navCornerIndex >= navCorners.Length;
        bool canRepathEarly = oldRouteFinished || Time.time - lastNavRepathTime >= Mathf.Min(interval, 0.2f);
        if (!hasNavDestination || Time.time >= nextNavRepathTime || (goalChanged && canRepathEarly))
        {
            // CalculatePath is synchronous; limit frequent recalculation for moving targets.
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

        // The NavMesh supplies the detour. TankPawn/TankMover still control how the hull drives.
        // Do not run wall-avoidance spins here: they can override an otherwise valid corner route.
        DriveToward(navCorners[navCornerIndex], false);
        return NavigationResult.Moving;
    }

    // Steering for both navigation corners and the original no-NavMesh fallback.
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

    protected void Chase()
    {
        if (!HasTargetMemory) { StopTank(); return; }
        // At firing distance, keep aiming instead of driving into the target's hull.
        if (TargetVisible && PlanarDistance(tank.transform.position, LastKnownPosition) <= attackRange * 0.85f) Attack();
        else MoveToward(LastKnownPosition, waypointTolerance);
    }

    protected void Attack()
    {
        tank.HandleMove(0f);
        if (!HasTargetMemory) { tank.HandleRotation(0f); return; }
        TurnToward(LastKnownPosition);
        if (CanFireAtTarget()) tank.HandleShoot();
    }

    protected bool Investigate() { return MoveToward(LastKnownPosition, waypointTolerance); }

    protected void Patrol()
    {
        if (patrolComplete || patrolPoints == null || patrolPoints.Length == 0) { StopTank(); return; }
        // Skip missing waypoints and avoid an infinite loop when every entry is empty.
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

    public Vector3 CalculateFleePoint(Vector3 dangerPosition)
    {
        Vector3 away = tank.transform.position - dangerPosition;
        away.y = 0f;
        float distance = away.magnitude;
        if (distance < 0.001f) away = -tank.transform.forward;
        // Closer threat -> smaller percentage -> larger inverse -> farther safe point.
        float inversePercentage = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, fleeDangerDistance));
        float minimum = Mathf.Max(0.1f, minimumFleeDistance);
        float maximum = Mathf.Max(minimum, maximumFleeDistance);
        float fleeDistance = Mathf.Lerp(minimum, maximum, inversePercentage);
        return tank.transform.position + away.normalized * fleeDistance;
    }

    protected void Flee() { MoveToward(CalculateFleePoint(LastKnownPosition), waypointTolerance); }
    protected float DistanceToLastKnown() { return PlanarDistance(tank.transform.position, LastKnownPosition); }
    protected static float PlanarDistance(Vector3 first, Vector3 second)
    {
        Vector3 difference = first - second; difference.y = 0f; return difference.magnitude;
    }

    protected override void OnDisable()
    {
        TargetVisible = false; TargetHeard = false;
        hasNavDestination = false;
        navCorners = null;
        navCornerIndex = 0;
        base.OnDisable();
    }

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
