
using UnityEngine;


public abstract class Pawn : MonoBehaviour
{
    
    [Min(0f)] public float moveSpeed = 5f;

    
    [Min(0f)] public float turnSpeed = 120f;


   
    public Controller OwnerController { get; private set; }

    public void SetController(Controller controller)
    {
       
        if (controller != null && OwnerController != null && OwnerController != controller)
        {
            OwnerController.enabled = false;
        }

        // set the owner controller to the passed in controller 
        OwnerController = controller;
    }

   
    protected virtual void OnEnable()
    {
        // if the instance of the game manager is not null register this pawn with the game manager
        if (GameManager.Instance != null) GameManager.Instance.Register(this);
    }

    public virtual void Start()
    {
        // aat the start of play if the instance of the game manager is not null register this pawn with the game manager 
        if (GameManager.Instance != null) GameManager.Instance.Register(this);
    }

    public virtual void Update() { }

    protected virtual void OnDisable()
    {
        // stop the pawns (presumed to be a movement stop when i implement it)
        Stop();

        // if the game manager instance is not null then unregister this pawn with it 
        if (GameManager.Instance != null) GameManager.Instance.Unregister(this);
    }

  
    public abstract void HandleMove(float moveInput);
    public abstract void HandleRotation(float rotationInput);
    public abstract bool HandleShoot();
    public abstract void Stop();
}
