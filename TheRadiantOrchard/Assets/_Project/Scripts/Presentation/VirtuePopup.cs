using RadiantOrchard.Core;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>One pooled virtue name above a healed visitor ("Love", "Joy"…; CLAUDE.md 5.6). Moved by VirtuePopups.</summary>
    public sealed class VirtuePopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        private Vector2 _start;
        private Color _colour;

        public void Show(string text, Color colour, Vector2 position)
        {
            _start = position;
            _colour = colour;
            _text.text = text;
            Set(0f, 0f);
        }

        /// <summary>k = 0..1 through its life: rises by riseDistance, fades out over the last half.</summary>
        public void Set(float k, float riseDistance)
        {
            transform.position = new Vector3(_start.x, _start.y + riseDistance * k, 0f);
            float alpha = k < 0.5f ? 1f : 1f - (k - 0.5f) * 2f;
            _text.color = new Color(_colour.r, _colour.g, _colour.b, _colour.a * alpha);
        }

        private void Awake()
        {
            if (_text == null)
                Log.Error($"VirtuePopup '{name}': Text must be assigned; rebuild it (Radiant Orchard > Setup > Build Placeholder Visitor).", this);
        }
    }
}
