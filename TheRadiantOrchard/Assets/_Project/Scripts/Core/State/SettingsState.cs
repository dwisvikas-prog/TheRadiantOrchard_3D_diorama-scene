using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>Player settings. Kept in the save file, not PlayerPrefs (ENGINEERING_GUIDE section 6).</summary>
    [Serializable]
    public sealed class SettingsState
    {
        [SerializeField] private bool _sound = true;
        [SerializeField] private bool _music = true;

        public bool Sound { get => _sound; set => _sound = value; }
        public bool Music { get => _music; set => _music = value; }
    }
}
