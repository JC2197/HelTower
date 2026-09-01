using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps a Camp UI root alive through GameScene, then removes it when MainMenu becomes active.
/// </summary>
public class GameplaySessionRoot : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene activeScene)
    {
        if (activeScene.name == mainMenuSceneName)
            Destroy(gameObject);
    }
}
