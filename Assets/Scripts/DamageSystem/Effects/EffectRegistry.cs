using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "EffectRegistry", menuName = "Effects/Effect Registry")]
public class EffectRegistry : ScriptableObject
{
    public List<EffectConfig> effects = new List<EffectConfig>();
    public EffectConfig Get(string effectID)
    {
        return effects.Find(e => e.effectID == effectID);
    }
}