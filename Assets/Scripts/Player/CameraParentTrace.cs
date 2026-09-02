using UnityEngine;

[DisallowMultipleComponent]
public class CameraParentTrace : MonoBehaviour
{
    private void OnTransformParentChanged()
    {
        Debug.LogWarning($"[CameraParentTrace] Camera '{gameObject.name}' parent changed to '{transform.parent?.name ?? "<root>"}'. scene={gameObject.scene.name}\n{StackTraceUtility.ExtractStackTrace()}");
    }
}
