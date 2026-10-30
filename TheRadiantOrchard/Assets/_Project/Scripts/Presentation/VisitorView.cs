using System.Collections;
using RadiantOrchard.Core;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// One pooled visitor figure (CLAUDE.md 5.4): grey body and head, a thought bubble with the requested fruit and a
    /// patience bar that runs green to red (hidden on the bench, where nobody waits against a clock). Healing turns
    /// them to the fruit's colour and they walk off coloured. On arrival a short speech line (their virtue's situation)
    /// shows above them for a few seconds.
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
        [SerializeField] private TextMeshPro _line;
        [Tooltip("Backing behind the speech line, sized to the text when it is shown.")]
        [SerializeField] private SpriteRenderer _lineBack;
        [SerializeField] private Vector2 _linePadding = new Vector2(0.4f, 0.25f);

        [Header("Look (placeholder)")]
        [SerializeField] private Color _grey = new Color(0.62f, 0.62f, 0.6f, 1f);
        [SerializeField] private Color _patienceFull = new Color(0.36f, 0.72f, 0.36f, 1f);
        [SerializeField] private Color _patienceHalf = new Color(0.95f, 0.8f, 0.25f, 1f);
        [SerializeField] private Color _patienceEmpty = new Color(0.85f, 0.3f, 0.25f, 1f);
        [SerializeField] private float _sweepSeconds = 0.4f;
        [Tooltip("The celebrate hop when healed (placeholder until the stick-figure animation).")]
        [SerializeField] private float _hopHeight = 0.4f;
        [SerializeField] private float _hopSeconds = 0.35f;

        private Vector3 _fillScale;
        private Vector3 _fillPosition;
        private float _shownPatience = -1f;
        private float _shownLineAlpha = -1f;
        private Color _lineBackColour;
        private Vector2 _lineBackUnitSize = Vector2.one;
        // Where the backing sprite's pivot sits inside it (0..1); placeholder blocks pivot at the bottom.
        private Vector2 _lineBackPivot = new Vector2(0.5f, 0.5f);
        private Coroutine _heal;
        private Coroutine _hop;
        private float _hopOffset;

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
            HideLine();
        }

        /// <summary>Shows what they say, the backing fitted to the text. Called once per arrival, never per frame.</summary>
        public void ShowLine(string text)
        {
            // Active first: an inactive text has no mesh to measure.
            _line.gameObject.SetActive(true);
            _lineBack.gameObject.SetActive(true);
            _line.text = text;
            _line.ForceMeshUpdate();
            Vector2 size = (Vector2)_line.textBounds.size + _linePadding;
            _lineBack.transform.localScale = new Vector3(size.x / _lineBackUnitSize.x, size.y / _lineBackUnitSize.y, 1f);
            Vector2 centre = (Vector2)_line.transform.localPosition + (Vector2)_line.textBounds.center;
            _lineBack.transform.localPosition = centre + Vector2.Scale(_lineBackPivot - new Vector2(0.5f, 0.5f), size);
            _shownLineAlpha = -1f;
            SetLineAlpha(1f);
        }

        /// <summary>1 = fully shown, 0 = gone (fading out).</summary>
        public void SetLineAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (Mathf.Abs(alpha - _shownLineAlpha) < 0.01f) return;
            _shownLineAlpha = alpha;
            _line.alpha = alpha;
            _lineBack.color = new Color(_lineBackColour.r, _lineBackColour.g, _lineBackColour.b, _lineBackColour.a * alpha);
        }

        public void HideLine()
        {
            if (_line.gameObject.activeSelf) _line.gameObject.SetActive(false);
            if (_lineBack.gameObject.activeSelf) _lineBack.gameObject.SetActive(false);
        }

        public bool LineShown => _line.gameObject.activeSelf;
        public string LineText => _line.text;

        /// <summary>The patience bar is shown on the path and hidden once their patience has run out.</summary>
        public void ShowPatienceBar(bool show)
        {
            GameObject bar = _patienceFill.parent.gameObject;
            if (bar.activeSelf != show) bar.SetActive(show);
        }

        public void MoveTo(Vector2 position) => transform.position = new Vector3(position.x, position.y + _hopOffset, 0f);

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
            HideLine();
            _bubble.SetActive(false);
            _patienceFill.parent.gameObject.SetActive(false);
            if (!isActiveAndEnabled) { SetBodyColour(colour); return; }
            if (_heal != null) StopCoroutine(_heal);
            _heal = StartCoroutine(Sweep(colour));
            if (_hop != null) StopCoroutine(_hop);
            _hop = StartCoroutine(Hop());
        }

        // A little jump for joy; the presenter's MoveTo adds the offset, so it rides along while they walk off.
        private IEnumerator Hop()
        {
            for (float t = 0f; t < _hopSeconds; t += Time.deltaTime)
            {
                _hopOffset = Mathf.Sin(t / _hopSeconds * Mathf.PI) * _hopHeight;
                yield return null;
            }
            _hopOffset = 0f;
            _hop = null;
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

        private void OnDisable()
        {
            _heal = null;
            _hop = null;
            _hopOffset = 0f;
        }

        private void Awake()
        {
            if (_body.Length == 0 || _bubble == null || _bubbleFruit == null || _patienceFill == null || _patienceFillRenderer == null
                || _popupAnchor == null || _tonicAnchor == null || _line == null || _lineBack == null)
            {
                Log.Error($"VisitorView '{name}': a reference is missing; rebuild it (Radiant Orchard > Setup > Build Visitors).", this);
                return;
            }
            _fillScale = _patienceFill.localScale;
            _fillPosition = _patienceFill.localPosition;
            _lineBackColour = _lineBack.color;
            Sprite back = _lineBack.sprite;
            if (back != null)
            {
                _lineBackUnitSize = back.rect.size / back.pixelsPerUnit;
                _lineBackPivot = new Vector2(back.pivot.x / back.rect.width, back.pivot.y / back.rect.height);
            }
        }
    }
}
