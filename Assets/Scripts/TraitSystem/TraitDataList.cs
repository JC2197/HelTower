using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A global registry of all TraitData assets available for rolling.
/// TraitRoller uses this list instead of pulling from a character's trait tree.
/// Organized into reusable TraitGroup ScriptableObjects.
/// </summary>
[CreateAssetMenu(fileName = "TraitDataList", menuName = "Traits/Trait Data List")]
public class TraitDataList : ScriptableObject
{
    [Tooltip("Groups of TraitData assets available for trait rolling")]
    public List<TraitGroup> traitGroups = new List<TraitGroup>();

    /// <summary>
    /// Flattened enumerable of all traits across all groups. Use this in place of nested foreach loops.
    /// </summary>
    public IEnumerable<TraitData> AllTraits => traitGroups.Where(g => g != null).SelectMany(g => g.traits);
}
