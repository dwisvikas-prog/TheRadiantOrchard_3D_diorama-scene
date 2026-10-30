using System.Collections;
using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// The single stand-in visitor of W1·21: a grey figure on the first waiting spot with a thought bubble showing
    /// the fruit they want. A heal turns them to colour for a moment, then they turn grey and ask again.
    /// Week 2's VisitorManager replaces this with real arrivals, patience, the bench and leaving (CLAUDE.md 5.4).
    /// </summary>
    public sealed class PlaceholderVisitor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] _body = new SpriteRenderer[0];
        [SerializeField] private GameObject _bubble;
        [SerializeField] private SpriteRenderer _bubbleFruit;
        [SerializeField] private Transform _popupAnchor;
        [Tooltip("Fruit id this visitor asks for, until VisitorManager picks requests (W2).")]
        [SerializeField] private string _requestedFruitId = "strawberry";

        [Header("Look (placeholder)")]
        [SerializeField] private Color _grey = new Color(0.62f, 0.62f, 0.6f, 1f);
        [SerializeField] private float _sweepSeconds = 0.4f;
        [SerializeField] private float _healedSeconds = 1.6f;

        private Coroutine _heal;

        public string RequestedFruitId => _requestedFruitId;
        public Vector2 PopupPosition => _popupAnchor.position;

        public void Init(GameConfig config)
        {
            FruitDefinition fruit = config.FindFruit(_requestedFruitId);
            if (fruit == null) Log.Error($"PlaceholderVisitor '{name}': unknown requested fruit '{_requestedFruitId}'.", this);
            else if (fruit.Icon != null)
            {
                _bubbleFruit.sprite = fruit.Icon;
                _bubbleFruit.color = Color.white;
            }
            else _bubbleFruit.color = fruit.Tint;
            ShowGrey();
        }

        /// <summary>Grey to colour (the colour sweep shader replaces this tint in W3), then back to waiting.</summary>
        public void PlayHeal(Color colour)
        {
            if (!isActiveAndEnabled) return;
            if (_heal != null) StopCoroutine(_heal);
            _heal = StartCoroutine(HealRoutine(colour));
        }

        private IEnumerator HealRoutine(Color colour)
        {
            _bubble.SetActive(false);
            for (float t = 0f; t < _sweepSeconds; t += Time.deltaTime)
            {
                SetBodyColour(Color.Lerp(_grey, colour, t / _sweepSeconds));
                yield return null;
            }
            SetBodyColour(colour);
            yield return new WaitForSeconds(_healedSeconds);
            ShowGrey();
            _heal = null;
        }

        private void ShowGrey()
        {
            SetBodyColour(_grey);
            _bubble.SetActive(true);
        }

        private void SetBodyColour(Color colour)
        {
            for (int i = 0; i < _body.Length; i++) _body[i].color = colour;
        }

        private void OnDisable()
        {
            _heal = null;
            if (_bubble != null) ShowGrey();
        }

        private void Awake()
        {
            if (_body.Length == 0 || _bubble == null || _bubbleFruit == null || _popupAnchor == null)
                Log.Error($"PlaceholderVisitor '{name}': a reference is missing; rebuild it (Radiant Orchard > Setup > Build Placeholder Visitor).", this);
        }
    }
}
