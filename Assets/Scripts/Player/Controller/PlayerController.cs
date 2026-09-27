using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class PlayerController : Controller
{
    private PlayerInput playerInput;

    private InputAction moveAction;

    private InputAction rotateAction;

    public TankPawn tankPawn;


    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];

        rotateAction = playerInput.actions["Rotate"];

    }


    // Start is called before the first frame update
    public override void Start()
    {
        // Run the Start() function from the parent (base) class
        base.Start();


      


        
    }

    

    // Update is called once per frame
    public override void Update()
    {

        // Run the Update() function from the parent (base) class
        base.Update();

        ProcessInputs();
    }
    private void OnEnable()
    {
        moveAction.Enable();
        rotateAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        rotateAction.Disable();

    }

    protected void ProcessInputs()
    {
        float moveInput = moveAction.ReadValue<float>();
        float rotateInput = rotateAction.ReadValue<float>();

        tankPawn.HandleMove(moveInput);

        tankPawn.HandleRotation(rotateInput);



    }
  

}