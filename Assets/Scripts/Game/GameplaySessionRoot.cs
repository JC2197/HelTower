using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lifetime marker for the single Bootstrap-owned gameplay session prefab.
/// </summary>
public class GameplaySessionRoot : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public static GameplaySessionRoot Instance { get; private set; }

    private void Awake()
    {
        BeginSession();
    }

    public bool BeginSession()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        if (Instance == this)
            return true;

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        return true;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        if (Instance == this)
            Instance = null;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
    {
        if (activeScene.name == mainMenuSceneName)
            Destroy(gameObject);
    }
}
