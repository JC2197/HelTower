using System.Collections.Generic;
using UnityEngine;

namespace HelTower.Characters
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "HelTower/Character Database")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        [SerializeField] private List<Class> characters = new List<Class>();

        public IReadOnlyList<Class> GetAllCharacters() => characters;
    }
}
