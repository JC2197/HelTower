using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private Color _backgroundColor = Color.black;
    [SerializeField] private float _orthographicSize = 6f;

    private Camera _activeCamera;

    public override void OnStartClient()
    => TryAttachCamera();

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    => TryAttachCamera();

    private void TryAttachCamera()
    {
        if (!IsOwner || _cameraHolder == null)
            return;

        if (_activeCamera == null)
            _activeCamera = Camera.main;

        if (_activeCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Camera");
            _activeCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        if (_activeCamera != null)
        {
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
}