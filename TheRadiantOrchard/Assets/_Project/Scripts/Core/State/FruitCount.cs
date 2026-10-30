using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// A fruit id with a count (the basket), and how many of them are golden. JsonUtility cannot serialize dictionaries,
    /// hence a list of these. Golden was added in W2·08; older saves load it as 0.
    /// </summary>
    [Serializable]
    public sealed class FruitCount
    {
        [SerializeField] private string _fruitId = string.Empty;
        [SerializeField] private int _count;
        [SerializeField] private int _golden;

        // For the serializer only.
        private FruitCount() { }

        public FruitCount(string fruitId, int count)
        {
            _fruitId = fruitId ?? string.Empty;
            _count = count;
        }

        public string FruitId => _fruitId;
        public int Count { get => _count; set => _count = value; }
        /// <summary>How many of Count are golden fruit (never more than Count).</summary>
        public int Golden { get => _golden; set => _golden = value; }
    }
}
