 // MilestoneTwoSceneValidator.cs - Editor-only checks for the references code cannot assign.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class MilestoneTwoSceneValidator
{
    [MenuItem("Tools/UATanks/Validate Milestone Two Scene")]
    public static void ValidateScene()
    {
        int errors = 0;
        int warnings = 0;
        GameManager[] managers = SceneComponents<GameManager>();
        Check(managers.Length == 1, "The scene needs exactly one active GameManager.", null, ref errors);
        PlayerController[] players = SceneComponents<PlayerController>();
        Check(players.Length > 0, "The scene needs a PlayerController.", null, ref errors);
        HashSet<int> playerTeams = new HashSet<int>();
        foreach (PlayerController player in players)
        {
            TankPawn pawn = player.pawn as TankPawn;
            if (pawn == null) pawn = player.tankPawn != null ? player.tankPawn : player.GetComponent<TankPawn>();
            Check(pawn != null, "PlayerController needs a TankPawn reference or a TankPawn on the same object.", player, ref errors);
            if (pawn != null) playerTeams.Add(pawn.team);
            PlayerInput input = player.GetComponent<PlayerInput>();
            bool validActions = input != null && input.actions != null
                && input.actions.FindAction("AM_Default/Move", false) != null
                && input.actions.FindAction("AM_Default/Rotate", false) != null
                && input.actions.FindAction("AM_Default/Fire", false) != null;
            Check(validActions, "Assign the supplied input asset (AM_Default: Move, Rotate, Fire) to PlayerInput.", player, ref errors);
        }
        Check(SceneComponents<HunterAIController>().Length > 0, "Add a HunterAIController tank.", null, ref errors);
        Check(SceneComponents<PatrolAIController>().Length > 0, "Add a PatrolAIController tank.", null, ref errors);
        Check(SceneComponents<CowardAIController>().Length > 0, "Add a CowardAIController tank.", null, ref errors);
        Check(SceneComponents<SentryAIController>().Length > 0, "Add a SentryAIController tank.", null, ref errors);
        foreach (TankPawn tank in SceneComponents<TankPawn>())
        {
            Check(tank.GetComponent<Rigidbody>() != null && tank.GetComponent<BoxCollider>() != null,
                "Every tank needs a root Rigidbody and solid BoxCollider.", tank, ref errors);
            BoxCollider hull = tank.GetComponent<BoxCollider>();
            Check(hull != null && hull.enabled && !hull.isTrigger, "The tank hull must be enabled and not a trigger.", tank, ref errors);
            Check(tank.Mover != null && tank.Shooter != null && tank.Health != null && tank.Noise != null,
                "Every tank needs TankMover, TankShooter, TankHealth, and TankNoise.", tank, ref errors);
            Check(tank.moveSpeed > 0f && tank.turnSpeed > 0f, "Move Speed and Turn Speed must be above zero (old saved values may still be zero).", tank, ref errors);
            if (tank.Shooter != null)
            {
                Check(tank.Shooter.projectilePrefab != null && tank.Shooter.firePoint != null,
                    "Assign a projectile prefab and FirePoint to TankShooter.", tank, ref errors);
                if (tank.Shooter.projectilePrefab != null)
                    Check(tank.Shooter.projectilePrefab.gameObject.activeSelf && tank.Shooter.projectilePrefab.enabled,
                        "The projectile prefab and its TankProjectile component must be enabled.", tank, ref errors);
                if (tank.Shooter.firePoint != null)
                    Check(Vector3.Angle(tank.transform.forward, tank.Shooter.firePoint.forward) < 1f,
                        "FirePoint blue Z arrow must face in the same direction as the tank's blue Z arrow.", tank, ref errors);
            }
            Controller[] controllers = tank.GetComponents<Controller>();
            Check(controllers.Length == 1, "Keep exactly one controller on each tank.", tank, ref errors);
        }
        foreach (AIController ai in SceneComponents<AIController>())
        {
            TankPawn tank = ai.pawn as TankPawn;
            if (tank == null) tank = ai.GetComponent<TankPawn>();
            Check(tank != null && ai.GetComponent<TankSenses>() != null, "Every AI needs TankPawn and TankSenses.", ai, ref errors);
            Check(tank != null && !playerTeams.Contains(tank.team), "Enemy tanks must use a different team number from every player.", ai, ref errors);
            Check(ai.hearingDistance > 0f && ai.hearingSensitivity > 0f && ai.viewDistance > 0f
                && ai.fieldOfView > 0f && ai.inSightsAngle > 0f && ai.attackRange > 0f,
                "AI hearing, view distance, cone angles, and attack range must be positive.", ai, ref errors);
            if (ai is PatrolAIController)
            {
                int assigned = 0;
                if (ai.patrolPoints != null) foreach (Transform point in ai.patrolPoints) if (point != null) assigned++;
                Check(assigned >= 2, "The patrol tank needs at least two assigned waypoints routed through the cube maze.", ai, ref errors);
            }
        }
        if (SceneManager.GetActiveScene().buildIndex < 0)
        {
            warnings++;
            Debug.LogWarning("Add the saved scene to Build Settings / Build Profiles Scene List so Restart level can load it.");
        }
        Debug.Log("Milestone Two scene validation: " + errors + " error(s), " + warnings
            + " warning(s). Next run the play-test checklist; this checks scene wiring only.");
    }

    private static void Check(bool passed, string message, Object context, ref int errors)
    {
        if (passed) return;
        errors++;
        Debug.LogError(message, context);
    }

    private static T[] SceneComponents<T>() where T : Component
    {
#if UNITY_2022_2_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
}
#endif
