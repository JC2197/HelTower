using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWeaponTraitTree", menuName = "Trait System/Weapon Trait Tree")]
public class WeaponTraitTree : ScriptableObject
{
    public string weaponName;
    public List<TraitNode> traitNodes = new List<TraitNode>();
    public List<TraitTreeConnection> connections = new List<TraitTreeConnection>();
}

[System.Serializable]
public class TraitNode
{
    public string NodeID;
    public TraitData traitData;
    public Vector2 position;
    public List<string> connectedNodeIDs = new List<string>();
}

[System.Serializable]
public class TraitTreeConnection
{
    public string connectionID;
    public string[] fromNodeIDs = new string[0];
    public string[] toNodeIDs = new string[0];
}

