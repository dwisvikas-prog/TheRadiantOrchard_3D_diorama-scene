using System.Collections;
using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// One pooled visitor figure (CLAUDE.md 5.4): grey body and head, a thought bubble with the requested fruit and a
    /// patience bar that runs green to red. Healing turns them to the fruit's colour and they walk off coloured.
    /// Placeholder shapes until the stick-figure art (CHAR) exists. Moved by VisitorsPresenter; no Update of its own.
    /// </summary>
    public sealed class VisitorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] _body = new SpriteRenderer[0];
        [SerializeField] private GameObject _bubble;
        [SerializeField] private SpriteRenderer _bubbleFruit;
        [SerializeField] private Transform _patienceFill;
        [SerializeField] private SpriteRenderer _patienceFillRenderer;
        [SerializeField] private Transform _popupAnchor;
        [SerializeField] private Transform _tonicAnchor;

        [Header("Look (placeholder)")]
        [SerializeField] private Color _grey = new Color(0.62f, 0.62f, 0.6f, 1f);
        [SerializeField] private Color _patienceFull = new Color(0.36f, 0.72f, 0.36f, 1f);
        [SerializeField] private Color _patienceHalf = new Color(0.95f, 0.8f, 0.25f, 1f);
        [SerializeField] private Color _patienceEmpty = new Color(0.85f, 0.3f, 0.25f, 1f);
        [SerializeField] private float _sweepSeconds = 0.4f;

        private Vector3 _fillScale;
        private Vector3 _fillPosition;
        private float _shownPatience = -1f;
        private Coroutine _heal;

        public Vector2 PopupPoint => _popupAnchor.position;
        public Vector2 TonicPoint => _tonicAnchor.position;

        /// <summary>A fresh grey visitor asking for this fruit (fruit icon when the art exists, else its colour).</summary>
        public void Show(FruitDefinition fruit)
        {
            if (_heal != null) StopCoroutine(_heal);
            _heal = null;
            SetBodyColour(_grey);
            _bubble.SetActive(true);
            _patienceFill.parent.gameObject.SetActive(true);
            if (fruit != null && fruit.Icon != null)
            {
                _bubbleFruit.sprite = fruit.Icon;
                _bubbleFruit.color = Color.white;
            }
            else _bubbleFruit.color = fruit != null ? fruit.Tint : Color.white;
            _shownPatience = -1f;
            SetPatience(1f);
        }

        public void MoveTo(Vector2 position) => transform.position = new Vector3(position.x, position.y, 0f);

        /// <summary>1 = full, 0 = run out. Shrinks the bar from the right and shifts green → yellow → red.</summary>
        public void SetPatience(float patience)
        {
            patience = Mathf.Clamp01(patience);
            if (Mathf.Abs(patience - _shownPatience) < 0.002f) return;
            _shownPatience = patience;
            _patienceFill.localScale = new Vector3(_fillScale.x * patience, _fillScale.y, _fillScale.z);
            _patienceFill.localPosition = new Vector3(_fillPosition.x - _fillScale.x * (1f - patience) * 0.5f * BarUnitWidth, _fillPosition.y, _fillPosition.z);
            _patienceFillRenderer.color = patience > 0.5f
                ? Color.Lerp(_patienceHalf, _patienceFull, (patience - 0.5f) * 2f)
                : Color.Lerp(_patienceEmpty, _patienceHalf, patience * 2f);
        }

        /// <summary>Grey to colour (the colour sweep shader replaces this tint in W3); stays coloured while leaving.</summary>
        public void PlayHeal(Color colour)
        {
            _bubble.SetActive(false);
            _patienceFill.parent.gameObject.SetActive(false);
            if (!isActiveAndEnabled) { SetBodyColour(colour); return; }
            if (_heal != null) StopCoroutine(_heal);
            _heal = StartCoroutine(Sweep(colour));
        }

        private float BarUnitWidth
        {
            get
            {
                Sprite s = _patienceFillRenderer.sprite;
                return s != null ? s.rect.width / s.pixelsPerUnit : 1f;
            }
        }

        private IEnumerator Sweep(Color colour)
        {
            for (float t = 0f; t < _sweepSeconds; t += Time.deltaTime)
            {
                SetBodyColour(Color.Lerp(_grey, colour, t / _sweepSeconds));
                yield return null;
            }
            SetBodyColour(colour);
            _heal = null;
        }

        private void SetBodyColour(Color colour)
        {
            for (int i = 0; i < _body.Length; i++) _body[i].color = colour;
        }

        private void OnDisable() => _heal = null;

        private void Awake()
        {
            if (_body.Length == 0 || _bubble == null || _bubbleFruit == null || _patienceFill == null || _patienceFillRenderer == null
                || _popupAnchor == null || _tonicAnchor == null)
            {
                Log.Error($"VisitorView '{name}': a reference is missing; rebuild it (Radiant Orchard > Setup > Build Visitors).", this);
                return;
            }
            _fillScale = _patienceFill.localScale;
            _fillPosition = _patienceFill.localPosition;
        }
    }
}
