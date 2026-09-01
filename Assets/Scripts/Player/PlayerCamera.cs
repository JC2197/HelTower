using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private Color _backgroundColor = Color.black;
    [SerializeField] private float _orthographicSize = 5f;

    private Camera _activeCamera;

    public override void OnStartClient()
    => TryAttachCamera();

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    => TryAttachCamera();

    private void OnEnable()
    {
        UnitySceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        UnitySceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previousScene, UnityEngine.SceneManagement.Scene activeScene)
    {
        if (IsOwner)
            StartCoroutine(AttachCameraAfterSceneChange());
    }

    private System.Collections.IEnumerator AttachCameraAfterSceneChange()
    {
        yield return null;
        _activeCamera = FindCameraInActiveScene();
        TryAttachCamera();
    }

    private void TryAttachCamera()
    {
        if (!IsOwner || _cameraHolder == null)
            return;

        if (_activeCamera == null)
            _activeCamera = FindCameraInActiveScene();

        if (_activeCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Camera");
            _activeCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        if (_activeCamera != null)
        {
            UniversalAdditionalCameraData cameraData = _activeCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;

            _activeCamera.gameObject.SetActive(true);
            _activeCamera.enabled = true;
            _activeCamera.tag = "MainCamera";
            _activeCamera.transform.SetParent(_cameraHolder, false);
            _activeCamera.transform.localPosition = new Vector3(0f, 0f, -10f);
            _activeCamera.transform.localRotation = Quaternion.identity;
            _activeCamera.clearFlags = CameraClearFlags.SolidColor;
            _activeCamera.backgroundColor = _backgroundColor;
            _activeCamera.orthographic = true;
            _activeCamera.orthographicSize = _orthographicSize;
            _activeCamera.depth = -1;
            _activeCamera.allowHDR = false;
            _activeCamera.allowMSAA = false;
            _activeCamera.cullingMask = -1;
        }
    }

    private static Camera FindCameraInActiveScene()
    {
        UnityEngine.SceneManagement.Scene activeScene = UnitySceneManager.GetActiveScene();
        foreach (GameObject rootObject in activeScene.GetRootGameObjects())
        {
            Camera camera = rootObject.GetComponentInChildren<Camera>(true);
            if (camera != null)
                return camera;
        }

        return Camera.main;
    }
}