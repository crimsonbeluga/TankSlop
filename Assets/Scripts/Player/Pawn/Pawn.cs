using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Pawn : MonoBehaviour
{
    // Variable for move speed
    public float moveSpeed;
    // Variable for turn speed
    public float turnSpeed;

    // Start is called before the first frame update
    public virtual void Start()
    {
    }

    // Update is called once per frame
    public virtual void Update()
    {
    }

    public abstract void HandleMove(float moveInput);
    public abstract void HandleRotation(float rotationInput);
    
}