using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// All player-facing text, key → English. Code and prefabs only ever hold keys, so wording (Bible translation,
    /// "Leave a prayer" vs "Make a wish", Wisdom Tree vs "the Vine") changes here without touching code.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/String Table", fileName = "StringTable")]
    public sealed class StringTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private string _key;
            [TextArea(1, 4)] [SerializeField] private string _text;

            public Entry(string key, string text)
            {
                _key = key;
                _text = text;
            }

            public string Key => _key;
            public string Text => _text;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];

        // Built on first lookup; never serialized, so nothing is written to the asset at runtime.
        [NonSerialized] private Dictionary<string, string> _lookup;
        [NonSerialized] private HashSet<string> _reportedMissing;

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>Text for the key, or "#key" (with one warning per key) so a missing string is visible on screen.</summary>
        public string Get(string key)
        {
            if (TryGet(key, out string text))
                return text;

            _reportedMissing ??= new HashSet<string>();
            if (_reportedMissing.Add(key ?? string.Empty))
                Log.Warn($"{name}: missing string key '{key}'.", this);
            return "#" + key;
        }

        public bool TryGet(string key, out string text)
        {
            if (string.IsNullOrEmpty(key))
            {
                text = null;
                return false;
            }

            if (_lookup == null)
                BuildLookup();
            return _lookup.TryGetValue(key, out text);
        }

        public bool Contains(string key) => TryGet(key, out _);

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, string>(_entries.Length, StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                string key = _entries[i].Key;
                if (!string.IsNullOrEmpty(key) && !_lookup.ContainsKey(key))
                    _lookup.Add(key, _entries[i].Text ?? string.Empty);
            }
        }

        private void OnEnable()
        {
            _lookup = null;
            _reportedMissing = null;
        }

        private void OnValidate()
        {
            _lookup = null;
            _reportedMissing = null;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                string key = _entries[i].Key;
                if (string.IsNullOrWhiteSpace(key))
                    Debug.LogError($"{name}: entry {i} has no key.", this);
                else if (!seen.Add(key))
                    Debug.LogError($"{name}: key '{key}' is listed twice.", this);
                else if (string.IsNullOrWhiteSpace(_entries[i].Text))
                    Debug.LogWarning($"{name}: key '{key}' has no text.", this);
            }
        }
    }
}
