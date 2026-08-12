using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Base authoring asset for passive abilities.
/// Stores passive-tunable data and resolves which runtime PassiveAbility component to attach.
/// </summary>
public class PassiveAbilityConfigBase : ScriptableObject
{
    [Tooltip("Optional visuals spawned when this passive starts.")]
    [SerializeField] private GameObject passiveVisualsPrefab;

    [Tooltip("Assembly-qualified runtime type name for the PassiveAbility MonoBehaviour to add at runtime.")]

    private string passiveRuntimeTypeName;
#if UNITY_EDITOR
    [Tooltip("Assign a MonoBehaviour script deriving from PassiveAbility. OnValidate stores its assembly-qualified type name.")]
    [SerializeField] private UnityEditor.MonoScript passiveRuntimeScript;

    private void OnValidate()
    {
        if (passiveRuntimeScript == null)
            return;

        Type scriptType = passiveRuntimeScript.GetClass();
        if (scriptType == null || !typeof(PassiveAbility).IsAssignableFrom(scriptType))
        {
            Debug.LogWarning($"[PassiveAbilityConfigBase] Script '{passiveRuntimeScript.name}' must derive from PassiveAbility.");
            return;
        }

        passiveRuntimeTypeName = scriptType.AssemblyQualifiedName;
    }
#endif

    public GameObject PassiveVisualsPrefab => passiveVisualsPrefab;

    public string PassiveRuntimeTypeName => passiveRuntimeTypeName;

    public virtual Type ResolveRuntimeType()
    {
        if (string.IsNullOrWhiteSpace(passiveRuntimeTypeName))
            return null;

        Type runtimeType = Type.GetType(passiveRuntimeTypeName);
        if (runtimeType != null)
            return runtimeType;

        // Legacy safety: also allow plain type names by searching loaded assemblies.
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            runtimeType = assembly.GetType(passiveRuntimeTypeName);
            if (runtimeType != null)
                return runtimeType;
        }

        return null;
    }

    public virtual PassiveAbility CreateRuntime(GameObject owner)
    {
        if (owner == null)
            return null;

        Type runtimeType = ResolveRuntimeType();
        if (runtimeType == null)
            return null;

        if (!typeof(PassiveAbility).IsAssignableFrom(runtimeType))
            return null;

        return owner.AddComponent(runtimeType) as PassiveAbility;
    }
}