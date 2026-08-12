using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A named group of TraitData assets. Create as a separate ScriptableObject asset
/// and assign to a TraitDataList for modular trait pool management.
/// </summary>
[CreateAssetMenu(fileName = "New Trait Group", menuName = "Traits/Trait Group")]
public class TraitGroup : ScriptableObject
{
    public List<TraitData> traits = new List<TraitData>();
}
