using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


// Run the manager early so controllers and pawns can register during their OnEnable callbacks.
// The scene still needs an active GameManager object; a class declaration does not create one.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public enum RoundState { Waiting, Playing, Victory, Defeat }
   
    // Instance belongs to the class, so any script can use GameManager.Instance without its own reference.
    // The private setter lets this class control which scene object is the shared manager.
    public static GameManager Instance { get; private set; }
    public RoundState State { get; private set; }
   
    // Expose controllers, player controllers, AI controllers, and pawns for other systems to read.
    // The private backing lists are changed through Register/Unregister to keep membership consistent.
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

   
    // Destroy duplicate managers, assign the surviving instance, and register objects already in the scene.
    // OnDestroy clears Instance only when this object is the current singleton.
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

 
    // Seed the registry from active scene objects at startup. Later changes are handled by lifecycle
    // callbacks in Controller and Pawn, so finding every object is not repeated every frame.
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

    
    // Reject null, disabled, and duplicate controllers. Add every controller to the base list and
    // also add player/AI subclasses to their matching lists. Each AI personality qualifies through inheritance.
    public void Register(Controller controller)
    {
        if (controller == null || !controller.isActiveAndEnabled || controllers.Contains(controller)) return;
        controllers.Add(controller);
        PlayerController player = controller as PlayerController;
        AIController ai = controller as AIController;
        if (player != null) { playerControllers.Add(player); hadPlayer = true; }
        if (ai != null) { aiControllers.Add(ai); hadEnemy = true; }
    }

 
    // Controller.OnDisable calls this when a controller is disabled or its object is removed.
    // Remove it from the general list and its player/AI list so dead enemies are no longer counted.
    public void Unregister(Controller controller)
    {
        controllers.Remove(controller);
        PlayerController player = controller as PlayerController;
        AIController ai = controller as AIController;
        if (player != null) playerControllers.Remove(player);
        if (ai != null) aiControllers.Remove(ai);
    }

   
    // Pawn.OnEnable/Start register active pawns; Pawn.OnDisable calls the matching Unregister overload.
    public void Register(Pawn pawn)
    {
        if (pawn != null && pawn.isActiveAndEnabled && !pawns.Contains(pawn)) pawns.Add(pawn);
    }

    public void Unregister(Pawn pawn) { pawns.Remove(pawn); }

    
    // Use the live controller lists to detect victory or defeat after a player and enemies have existed.
    // TankHUD displays the result and calls RestartLevel; demonstrate this complete loop in the video.
    private void Update()
    {
        if (State == RoundState.Victory || State == RoundState.Defeat) return;
        if (hadPlayer && playerControllers.Count == 0) State = RoundState.Defeat;
        else if (hadPlayer && hadEnemy && aiControllers.Count == 0) State = RoundState.Victory;
        else if (hadPlayer && hadEnemy) State = RoundState.Playing;
    }

   
    // Reload the active scene for another round. The scene must be included in the build scene list.
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
