// TankNoise.cs - Designer-tuned engine noise plus a fading shot noise pulse.
using UnityEngine;

[DisallowMultipleComponent]
public class TankNoise : MonoBehaviour
{
    [Min(0f)] public float movementNoise = 8f;
    [Min(0f)] public float shootingNoise = 20f;
    [Min(0.01f)] public float shotNoiseDuration = 1f;
    private float movementAmount;
    private float lastShotTime = float.NegativeInfinity;

    public float CurrentNoise
    {
        get
        {
            float shot = shootingNoise * Mathf.Clamp01(1f - (Time.time - lastShotTime) / Mathf.Max(0.01f, shotNoiseDuration));
            return Mathf.Max(0f, movementNoise) * movementAmount + Mathf.Max(0f, shot);
        }
    }

    public void SetMovement(float amount) { movementAmount = Mathf.Clamp01(amount); }
    public void MakeShotNoise() { lastShotTime = Time.time; }
    private void OnDisable() { movementAmount = 0f; lastShotTime = float.NegativeInfinity; }
}
