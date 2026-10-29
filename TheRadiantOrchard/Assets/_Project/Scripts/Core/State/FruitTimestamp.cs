using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>A fruit id with a Unix-ms UTC timestamp (bloom fruit regrow times).</summary>
    [Serializable]
    public sealed class FruitTimestamp
    {
        [SerializeField] private string _fruitId = string.Empty;
        [SerializeField] private long _utc;

        // For the serializer only.
        private FruitTimestamp() { }

        public FruitTimestamp(string fruitId, long utc)
        {
            _fruitId = fruitId ?? string.Empty;
            _utc = utc;
        }

        public string FruitId => _fruitId;
        public long Utc { get => _utc; set => _utc = value; }
    }
}
