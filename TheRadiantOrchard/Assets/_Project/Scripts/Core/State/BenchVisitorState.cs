using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>A visitor sitting on the bench: what they want and when they sat down (Unix ms UTC).</summary>
    [Serializable]
    public sealed class BenchVisitorState
    {
        [SerializeField] private string _fruitId = string.Empty;
        [SerializeField] private long _arrivedUtc;

        // For the serializer only.
        private BenchVisitorState() { }

        public BenchVisitorState(string fruitId, long arrivedUtc)
        {
            _fruitId = fruitId ?? string.Empty;
            _arrivedUtc = arrivedUtc;
        }

        public string FruitId => _fruitId;
        public long ArrivedUtc => _arrivedUtc;
    }
}
