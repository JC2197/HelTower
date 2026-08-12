#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TraitDataList))]
public class TraitDataListEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TraitDataList target2 = (TraitDataList)target;
        int totalCount = 0;
        foreach (var group in target2.traitGroups)
        {
            if (group != null)
                totalCount += group.traits.Count;
        }
        EditorGUILayout.HelpBox($"Currently contains {totalCount} traits across {target2.traitGroups.Count} groups.", MessageType.Info);
    }
}
#endif
