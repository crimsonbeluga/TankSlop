// GameManager.cs - Scene singleton and automatic registries for all active actors.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public enum RoundState { Waiting, Playing, Victory, Defeat }
    public static GameManager Instance { get; private set; }
    public RoundState State { get; private set; }
    public IReadOnlyList<Controller> Controllers { get { return controllers; } }
    public IReadOnlyList<PlayerController> PlayerControllers { get { return playerControllers; } }
    public IReadOnlyList<AIController> AIControllers { get { return aiControllers; } }
    public IReadOnlyList<Pawn> Pawns { get { return pawns; } }

    private readonly List<Controller> controllers = new List<Controller>();
    private readonly List<PlayerController> playerControllers = new List<PlayerController>();
    private readonly List<AIController> aiControllers = new List<AIController>();
    private readonly List<Pawn> pawns = new List<Pawn>();
    private bool hadPlayer;
    private bool hadEnemy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() { Instance = null; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RebuildLists();
    }

    public void RebuildLists()
    {
        controllers.Clear();
        playerControllers.Clear();
        aiControllers.Clear();
        pawns.Clear();
#if UNITY_2022_2_OR_NEWER
        Controller[] sceneControllers = FindObjectsByType<Controller>(FindObjectsSortMode.None);
        Pawn[] scenePawns = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
#else
        Controller[] sceneControllers = FindObjectsOfType<Controller>();
        Pawn[] scenePawns = FindObjectsOfType<Pawn>();
#endif
        foreach (Controller controller in sceneControllers) Register(controller);
        foreach (Pawn pawn in scenePawns) Register(pawn);
    }

    public void Register(Controller controller)
    {
        if (controller == null || !controller.isActiveAndEnabled || controllers.Contains(controller)) return;
        controllers.Add(controller);
        PlayerController player = controller as PlayerController;
        AIController ai = controller as AIController;
        if (player != null) { playerControllers.Add(player); hadPlayer = true; }
        if (ai != null) { aiControllers.Add(ai); hadEnemy = true; }
    }

    public void Unregister(Controller controller)
    {
        controllers.Remove(controller);
        PlayerController player = controller as PlayerController;
        AIController ai = controller as AIController;
        if (player != null) playerControllers.Remove(player);
        if (ai != null) aiControllers.Remove(ai);
    }

    public void Register(Pawn pawn)
    {
        if (pawn != null && pawn.isActiveAndEnabled && !pawns.Contains(pawn)) pawns.Add(pawn);
    }

    public void Unregister(Pawn pawn) { pawns.Remove(pawn); }

    private void Update()
    {
        if (State == RoundState.Victory || State == RoundState.Defeat) return;
        if (hadPlayer && playerControllers.Count == 0) State = RoundState.Defeat;
        else if (hadPlayer && hadEnemy && aiControllers.Count == 0) State = RoundState.Victory;
        else if (hadPlayer && hadEnemy) State = RoundState.Playing;
    }

    public void RestartLevel()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
