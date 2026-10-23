// Controller.cs - Owns a pawn; concrete controllers decide which commands to send.
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

public abstract class Controller : MonoBehaviour
{
    [Tooltip("The pawn this controller commands. Uses the pawn on this object if empty.")]

    // public reference to the Pawn
    public Pawn pawn;

    //called when this compenent wakes
    protected virtual void Awake()
    {
        // if the pawn is null get the pawn component and set it
        if (pawn == null) pawn = GetComponent<Pawn>();

        // if the pawn is not null set its controller to this  to ensure we set its controller (see parent class for understanding)
        if (pawn != null) pawn.SetController(this);
    }

    protected virtual void OnEnable()
    {
       
        // if the game manager is not null register this controller with it 
        if (GameManager.Instance != null) GameManager.Instance.Register(this);
    }

    public virtual void Start()
    {
        // Also supports a manager created after this controller was enabled.
        if (GameManager.Instance != null) GameManager.Instance.Register(this);
    }

    public virtual void Update() { }

    protected virtual void OnDisable()
    {
        // when this component is disabled if there is a pawn being controlled stop it
        if (pawn != null) pawn.Stop();

        // if the game manager instance is not null  unregister this controller with it
        if (GameManager.Instance != null) GameManager.Instance.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        // when this object is destroyed and the pawn is not null and the pawns owner controller is this controller  set the pawns controller to 
        //now be null
        if (pawn != null && pawn.OwnerController == this) pawn.SetController(null);
    }
}
