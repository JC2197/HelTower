using UnityEngine;

/// <summary>
/// Ensures CharacterSelectionManager exists. Add this to MainMenu or GameScene.
/// </summary>
public class CharacterSelectionManagerBootstrap : MonoBehaviour
{
    [SerializeField] private CharacterSelectionConfig config;
    
    private void Awake()
    {
        // Only create if doesn't exist
        if (CharacterSelectionManager.Instance == null)
        {
            GameObject managerObj = new GameObject("CharacterSelectionManager");
            CharacterSelectionManager manager = managerObj.AddComponent<CharacterSelectionManager>();
            
            // Assign config if we have one
            if (config != null)
            {
                var configField = typeof(CharacterSelectionManager).GetField("config", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                configField?.SetValue(manager, config);
            }
            
            Debug.Log("[Bootstrap] Created CharacterSelectionManager");
        }
        else
        {
            Debug.Log("[Bootstrap] CharacterSelectionManager already exists");
        }
    }
}
