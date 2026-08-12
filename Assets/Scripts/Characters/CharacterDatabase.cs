using System.Collections.Generic;
using UnityEngine;

namespace HelTower.Characters
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "HelTower/Character Database")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private List<ClassData> characters = new List<ClassData>();

        public IReadOnlyList<ClassData> GetAllCharacters() => characters;
    }
}
