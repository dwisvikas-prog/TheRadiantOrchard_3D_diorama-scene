using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// One virtue of the Fruit of the Spirit (CLAUDE.md 4): its name and the short situation lines a visitor who needs
    /// it says on arrival (5.4; 3 per virtue, client text, placeholders until then). Quiz scenarios join in W3.
    /// All text is StringTable keys. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Virtue Definition", fileName = "Virtue_")]
    public sealed class VirtueDefinition : ScriptableObject
    {
        [Tooltip("Stable lowercase id, e.g. 'self_control'.")]
        [SerializeField] private string _id;
        [Tooltip("StringTable key for the virtue's name (the heal pop-up).")]
        [SerializeField] private string _nameKey;
        [Tooltip("StringTable keys for the lines a visitor may say on arrival; one is picked at random.")]
        [SerializeField] private string[] _situationLineKeys = new string[0];

        public string Id => _id;
        public string NameKey => _nameKey;
        public IReadOnlyList<string> SituationLineKeys => _situationLineKeys;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
                Debug.LogWarning($"VirtueDefinition '{name}' has no id.", this);
            if (_situationLineKeys.Length == 0)
                Debug.LogWarning($"VirtueDefinition '{name}' has no situation lines; its visitors arrive silently.", this);
        }
    }
}
