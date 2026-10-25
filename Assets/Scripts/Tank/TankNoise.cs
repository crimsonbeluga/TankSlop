

using UnityEngine;

[DisallowMultipleComponent]
public class TankNoise : MonoBehaviour
{
   
    // Expose movementNoise and shootingNoise so designers control how loudly each pawn's actions register.
    // shotNoiseDuration controls how long a firing pulse remains detectable.
    [Min(0f)] public float movementNoise = 8f;
    [Min(0f)] public float shootingNoise = 20f;
    [Min(0.01f)] public float shotNoiseDuration = 1f;
    private float movementAmount;
    private float lastShotTime = float.NegativeInfinity;

    
    // Combine movement noise scaled by the reported movement amount with a shot pulse that fades over time.
    // AIController.CanHear converts this total into an effective radius using the AI's hearing sensitivity.
    public float CurrentNoise
    {
        get
        {
            float shot = shootingNoise * Mathf.Clamp01(1f - (Time.time - lastShotTime) / Mathf.Max(0.01f, shotNoiseDuration));
            return Mathf.Max(0f, movementNoise) * movementAmount + Mathf.Max(0f, shot);
        }
    }

    
    // TankMover calls SetMovement after accepted translation/turning; TankShooter calls MakeShotNoise after firing.
    // This is simulated noise data for AI, not an audio playback requirement.
    public void SetMovement(float amount) { movementAmount = Mathf.Clamp01(amount); }
    public void MakeShotNoise() { lastShotTime = Time.time; }
    private void OnDisable() { movementAmount = 0f; lastShotTime = float.NegativeInfinity; }
}
