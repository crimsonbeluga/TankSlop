
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI.Table;


[RequireComponent(typeof(PlayerInput))]
public class PlayerController : Controller
{
    // punblic reference to the controlled tank pawn 
    public TankPawn tankPawn;
  
    [Header("Designer keyboard controls")]
    public bool useInspectorKeyBindings = true;

    // public references to the movement keys and fire keys
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode fireKey = KeyCode.Space;

    //private reference to the player input system
    private PlayerInput playerInput;

    //private references to Input actions that handle starting the input chainn we will use delegates to bind and unbind when neccasary
    private InputAction moveAction;
    private InputAction rotateAction;
    private InputAction fireAction;

    // private floats representing the input values for movement and rotation
    private float moveInput;
    private float rotateInput;


    // bool representing if the fire input was held or not
    private bool fireInput;

    // a boolean to represent if we have called the started function yet
    private bool started;

    protected override void Awake()
    {
        // if the pawn is null and the tank pawn is not null the set the pawn to reference the tank pawn
        if (pawn == null && tankPawn != null) pawn = tankPawn;

        
        base.Awake();

        // attempt to cast pawn  as the derived child class tank pawn and set are tank pawn reference to that
        tankPawn = pawn as TankPawn;

        //create a reference that stores the player input component 
        playerInput = GetComponent<PlayerInput>();

        // We subscribe directly, so PlayerInput must not also send OnMove/OnFire messages.
        // This line tells Unity's Input System how to notify my scripts when an input action occurs.
        // to go more in depth because we struggled with this: 
        //playerInput — Your reference to Unity's PlayerInput component (DUH LMAO)
        //notificationBehavior — A property that determines how PlayerInput communicates input events.
        //PlayerNotifications.InvokeCSharpEvents — Tells Unity to use C# events instead of automatically calling methods by name.
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
    }

    public override void Start()
    {
        // call the parent class implementation of start before doing anything else
        base.Start();

        // set started to true cause we have started
        started = true;

        // handle the set up of all of are input thrown into one function
        BindInput();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // Start waits until every PlayerInput.OnEnable has prepared its private action copy.

        // if we have begun play bind input 
        if (started) BindInput();
    }

   
    private void BindInput()
    {
        // unbind all input to prevent stale event subscriptions 
        UnbindInput();

        // if there is no player input AND there are no input actions
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError("Assign Input Actions Asset to PlayerInput on the player tank.", this);
            return;
        }


        moveAction = playerInput.actions.FindAction("AM_Default/Move", false);
        rotateAction = playerInput.actions.FindAction("AM_Default/Rotate", false);
        fireAction = playerInput.actions.FindAction("AM_Default/Fire", false);
        if (moveAction == null || rotateAction == null || fireAction == null)
        {
            Debug.LogError("PlayerInput needs AM_Default with Move (Axis), Rotate (Axis), and Fire (Button). Import the supplied input asset.", this);
            return;
        }
        if (useInspectorKeyBindings)
        {
            // Override only keyboard bindings; the mouse fire binding remains available.
            OverrideAxis(moveAction, backwardKey, forwardKey);
            OverrideAxis(rotateAction, leftKey, rightKey);
            for (int i = 0; i < fireAction.bindings.Count; i++)
            {
                if (!string.IsNullOrEmpty(fireAction.bindings[i].path) && fireAction.bindings[i].path.StartsWith("<Keyboard>/"))
                    fireAction.ApplyBindingOverride(i, KeyPath(fireKey));
            }
        }
        // mass bind are input functions to each of there respective delegates
        moveAction.performed += OnMovePerformed;
        moveAction.canceled += OnMoveCanceled;
        rotateAction.performed += OnRotatePerformed;
        rotateAction.canceled += OnRotateCanceled;
        fireAction.performed += OnFirePerformed;
        fireAction.canceled += OnFireCanceled;

        // enable this input actions to be used
        moveAction.Enable();
        rotateAction.Enable();
        fireAction.Enable();
    }


    private static void OverrideAxis(InputAction action, KeyCode negative, KeyCode positive)
    {

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (!action.bindings[i].isPartOfComposite || string.IsNullOrEmpty(action.bindings[i].path)
                || !action.bindings[i].path.StartsWith("<Keyboard>/")) continue;
            action.ApplyBindingOverride(i, KeyPath(action.bindings[i].name == "negative" ? negative : positive));
        }
    }

    private static string KeyPath(KeyCode key)
    {
        if (key == KeyCode.None) return "";
        string name = key.ToString().ToLowerInvariant();
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) name = ((int)key - (int)KeyCode.Alpha0).ToString();
        if (name.StartsWith("keypad")) name = "numpad" + name.Substring(6);
        switch (key)
        {
            case KeyCode.Return: name = "enter"; break;
            case KeyCode.LeftControl: name = "leftCtrl"; break;
            case KeyCode.RightControl: name = "rightCtrl"; break;
        }
        return "<Keyboard>/" + name;
    }

    
    public override void Update()
    {
        base.Update();
        if (tankPawn == null || !tankPawn.IsAlive) return;
        tankPawn.HandleMove(moveInput);
        tankPawn.HandleRotation(rotateInput);
        if (fireInput) tankPawn.HandleShoot();
    }

    
    private void UnbindInput()
    {
        if (moveAction != null) { moveAction.performed -= OnMovePerformed; moveAction.canceled -= OnMoveCanceled; moveAction.Disable(); }
        if (rotateAction != null) { rotateAction.performed -= OnRotatePerformed; rotateAction.canceled -= OnRotateCanceled; rotateAction.Disable(); }
        if (fireAction != null) { fireAction.performed -= OnFirePerformed; fireAction.canceled -= OnFireCanceled; fireAction.Disable(); }
        moveInput = 0f;
        rotateInput = 0f;
        fireInput = false;
    }

    protected override void OnDisable() { UnbindInput(); base.OnDisable(); }
    private void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        moveInput = 0f; rotateInput = 0f; fireInput = false;
        if (tankPawn != null) tankPawn.Stop();
    }
    private void OnMovePerformed(InputAction.CallbackContext context) { moveInput = context.ReadValue<float>(); }
    private void OnMoveCanceled(InputAction.CallbackContext context) { moveInput = 0f; }
    private void OnRotatePerformed(InputAction.CallbackContext context) { rotateInput = context.ReadValue<float>(); }
    private void OnRotateCanceled(InputAction.CallbackContext context) { rotateInput = 0f; }
    private void OnFirePerformed(InputAction.CallbackContext context) { fireInput = true; }
    private void OnFireCanceled(InputAction.CallbackContext context) { fireInput = false; }
}
