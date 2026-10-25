

using UnityEngine;

public class TankHUD : MonoBehaviour
{
    
    // Read the singleton's live player/AI lists to show current health and remaining enemies.
   
    private void OnGUI()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        GUILayout.BeginArea(new Rect(12f, 12f, 330f, 180f), GUI.skin.box);
        GUILayout.Label("Tank Slop");
        foreach (PlayerController player in manager.PlayerControllers)
        {
            TankPawn tank = player != null ? player.pawn as TankPawn : null;
            if (tank != null && tank.Health != null) GUILayout.Label("Health: " + Mathf.CeilToInt(tank.Health.CurrentHealth) + " / " + Mathf.CeilToInt(tank.Health.maxHealth));
        }
        GUILayout.Label("Enemies remaining: " + manager.AIControllers.Count);
        GUILayout.Label("W/S move | A/D turn | Space / left mouse fire");
        if (manager.State == GameManager.RoundState.Victory || manager.State == GameManager.RoundState.Defeat)
        {
            GUILayout.Label(manager.State == GameManager.RoundState.Victory ? "All enemy tanks destroyed!" : "Your tank was destroyed.");
            if (GUILayout.Button("Restart level")) manager.RestartLevel();
        }
        GUILayout.EndArea();
    }
}
