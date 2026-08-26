using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TraitTree", menuName = "Trait System/Trait Tree")]
public class TraitTree : ScriptableObject
{
    public string treeName;
    public string description;

    public float nodeSpacing = 100f;
    public int nodeIconSize = 17;

    [Tooltip("Frame sprite drawn behind every node icon in this tree.")]
    public Sprite nodeIconFrame;

    public int canvasWidth = 400;
    public List<TraitNode> nodes = new List<TraitNode>();
    public int canvasHeight = 225;
    [Tooltip("Minimum canvas width used by auto canvas sizing.")]
    public int minCanvasWidth = 400;

    [Tooltip("Minimum canvas height used by auto canvas sizing.")]
    public int minCanvasHeight = 225;
    [Tooltip("Automatically size canvas bounds to node layout in the editor.")]
    public bool autoCanvasSize = true;
    public int autoCanvasPadding = 80;
    public List<TraitTreeConnection> connections = new List<TraitTreeConnection>();
}

//Trait nodes are the literal on-tree trait representations, containing data, position, cost, and connection info.
[System.Serializable]
public class TraitNode
{
    public string nodeID;
    public TraitData traitData;
    public Vector2 position;
    public List<string> connectedNodeIDs = new List<string>();
}



