using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>A fruit id with a count (the basket). JsonUtility cannot serialize dictionaries, hence a list of these.</summary>
    [Serializable]
    public sealed class FruitCount
    {
        [SerializeField] private string _fruitId = string.Empty;
        [SerializeField] private int _count;

        // For the serializer only.
        private FruitCount() { }

        public FruitCount(string fruitId, int count)
        {
            _fruitId = fruitId ?? string.Empty;
            _count = count;
        }

        public string FruitId => _fruitId;
        public int Count { get => _count; set => _count = value; }
    }
}
