using System.Collections.Generic;
using JoeConticello.ModularCombatCore;
using UnityEngine;

namespace HelTower.Characters
{
    [CreateAssetMenu(fileName = "Class", menuName = "HelTower/Class")]
    public sealed class Class : ScriptableObject
    {
        [SerializeField] private string className;
        [SerializeField] private AnimatorOverrideController characterAnimator;
        [SerializeField] private StatContainer statContainer;
        
        public string ClassName => className;
        public AnimatorOverrideController CharacterAnimator => characterAnimator;
        public StatContainer StatContainer => statContainer;

    }
}
    