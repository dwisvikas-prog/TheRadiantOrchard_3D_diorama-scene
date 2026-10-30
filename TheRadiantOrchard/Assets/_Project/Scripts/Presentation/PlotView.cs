using System.Collections;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows one plot: soil, tree, fruit and the growth countdown. Uses the fruit's own stage sprites when the art exists; until then a
    /// tinted placeholder block sized per stage. Never changes game state itself; it only calls PlotManager.
    /// </summary>
    public sealed class PlotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _soil;
        [SerializeField] private SpriteRenderer _tree;
        [SerializeField] private SpriteRenderer _fruit;
        [SerializeField] private GameObject _timer;
        [SerializeField] private TMP_Text _timerText;

        [Header("Placeholder look (until tree art exists)")]
        [SerializeField] private Color _lockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Vector2 _saplingSize = new Vector2(0.5f, 0.9f);
        [SerializeField] private Vector2 _growingSize = new Vector2(1.3f, 2.6f);
        [SerializeField] private Color _saplingColour = new Color(0.50f, 0.69f, 0.35f, 1f);
        [SerializeField] private Color _growingColour = new Color(0.31f, 0.56f, 0.23f, 1f);

        [Header("Wrong-gesture wobble (CLAUDE.md 5.5)")]
        [SerializeField] private float _wobbleDegrees = 8f;
        [SerializeField] private float _wobbleSeconds = 0.4f;
        [SerializeField] private int _wobbleSwings = 3;

        private Sprite _placeholderTree;
        private int _index = -1;
        private PlotManager _plots;
        private Coroutine _wobble;

        public int Index => _index;

        /// <summary>Where this plot's fruit hangs: the tonic takes off from here.</summary>
        public Vector2 FruitPosition => _fruit.transform.position;

        public void Bind(int index, PlotManager plots)
        {
            _index = index;
            _plots = plots;
            if (_placeholderTree == null) _placeholderTree = _tree.sprite;
        }

        public void Show(PlotInfo info, FruitDefinition fruit)
        {
            _soil.color = info.Unlocked ? Color.white : _lockedTint;
            _timer.SetActive(info.IsGrowing);

            if (info.IsEmpty || fruit == null)
            {
                _tree.enabled = false;
                _fruit.enabled = false;
                return;
            }

            Sprite art = info.Stage == TreeStage.Sapling ? fruit.SaplingSprite
                : info.Stage == TreeStage.Ripe ? fruit.RipeSprite
                : fruit.GrowingSprite;

            _tree.enabled = true;
            if (art != null)
            {
                _tree.sprite = art;
                _tree.color = Color.white;
                _tree.transform.localScale = Vector3.one;
                _fruit.enabled = false;
                return;
            }

            _tree.sprite = _placeholderTree;
            bool sapling = info.Stage == TreeStage.Sapling;
            SizeTo(_tree, sapling ? _saplingSize : _growingSize);
            _tree.color = sapling ? _saplingColour : _growingColour;
            _fruit.enabled = info.IsRipe;
            _fruit.color = fruit.Tint;
        }

        /// <summary>
        /// Whether a world point is on this plot's tree or fruit. Trees stand tall above the soil, and players swipe
        /// and tap the tree itself, so the tree counts as well as the plot's ground cells.
        /// </summary>
        public bool TreeContains(Vector2 world)
        {
            return (_tree.enabled && Contains(_tree.bounds, world)) || (_fruit.enabled && Contains(_fruit.bounds, world));
        }

        /// <summary>Shakes the tree from its base: the wrong-gesture reaction.</summary>
        public void Wobble()
        {
            if (!isActiveAndEnabled || !_tree.enabled) return;
            if (_wobble != null) StopCoroutine(_wobble);
            _wobble = StartCoroutine(WobbleRoutine());
        }

        private IEnumerator WobbleRoutine()
        {
            Transform t = _tree.transform;
            for (float elapsed = 0f; elapsed < _wobbleSeconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / _wobbleSeconds;
                float angle = Mathf.Sin(k * _wobbleSwings * 2f * Mathf.PI) * _wobbleDegrees * (1f - k);
                t.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            t.localRotation = Quaternion.identity;
            _wobble = null;
        }

        private static bool Contains(Bounds bounds, Vector2 world) =>
            world.x >= bounds.min.x && world.x <= bounds.max.x && world.y >= bounds.min.y && world.y <= bounds.max.y;

        private void OnDisable()
        {
            _wobble = null;
            if (_tree != null) _tree.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Sets the countdown text; the presenter calls this only when the shown second changes.</summary>
        public void SetTimerText(string text) => _timerText.text = text;

        private static void SizeTo(SpriteRenderer renderer, Vector2 worldSize)
        {
            Sprite s = renderer.sprite;
            if (s == null) return;
            float unitW = s.rect.width / s.pixelsPerUnit, unitH = s.rect.height / s.pixelsPerUnit;
            renderer.transform.localScale = new Vector3(worldSize.x / unitW, worldSize.y / unitH, 1f);
        }

        private void Awake()
        {
            if (_soil == null || _tree == null || _fruit == null || _timer == null || _timerText == null)
                Log.Error($"PlotView '{name}': Soil, Tree, Fruit and Timer must be assigned; rebuild the farm.", this);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Dev shortcuts until the dev panel (W2·02): right-click the component in Play mode.
        [ContextMenu("Dev/Plant strawberry")] private void DevPlantStrawberry() => DevPlant("strawberry");
        [ContextMenu("Dev/Plant pineapple")] private void DevPlantPineapple() => DevPlant("pineapple");
        [ContextMenu("Dev/Ripen now")] private void DevRipen() => Log.Info($"Ripen plot {_index + 1}: {_plots?.RipenNow(_index)}");
        [ContextMenu("Dev/Harvest")] private void DevHarvest() => Log.Info($"Harvest plot {_index + 1}: {(_plots != null && _plots.TryHarvest(_index, out _))}");

        private void DevPlant(string fruitId)
        {
            if (_plots == null) { Log.Warn("PlotView: not bound yet; use this in Play mode."); return; }
            Log.Info($"Plant {fruitId} on plot {_index + 1}: {_plots.TryPlant(_index, fruitId)}");
        }
#endif
    }
}
