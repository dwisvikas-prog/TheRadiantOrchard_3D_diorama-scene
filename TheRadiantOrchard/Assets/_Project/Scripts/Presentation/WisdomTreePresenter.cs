using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Lights the Wisdom Tree while a quiz is waiting and puts it out once answered. Shows the bloom (CLAUDE.md 5.9): at
    /// once for a save that already bloomed; reached in play it waits (pending) until the celebration calls
    /// RevealBloom while the camera looks at it. Fades picked fruits until they grow back. Dispose to unsubscribe.
    /// </summary>
    public sealed class WisdomTreePresenter : IDisposable
    {
        private readonly WisdomTreeView _view;
        private readonly WisdomTreeBloom _bloom;
        private readonly GameConfig _config;
        private readonly EventBus _events;

        public WisdomTreePresenter(WisdomTreeView view, QuizManager quiz, WisdomTreeBloom bloom, GameConfig config, EventBus events)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _bloom = bloom ?? throw new ArgumentNullException(nameof(bloom));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));

            _view.SetGlowing(quiz != null && quiz.IsReady);
            _view.SetFruits(_config.Fruits);
            _view.SetBloom(_bloom.IsActive, false);
            RefreshFruits();

            _events.Subscribe<QuizReady>(OnReady);
            _events.Subscribe<QuizAnswered>(OnAnswered);
            _events.Subscribe<BloomStarted>(OnBloomStarted);
            _events.Subscribe<BloomFruitChanged>(OnFruitChanged);
        }

        public void Dispose()
        {
            _events.Unsubscribe<QuizReady>(OnReady);
            _events.Unsubscribe<QuizAnswered>(OnAnswered);
            _events.Unsubscribe<BloomStarted>(OnBloomStarted);
            _events.Unsubscribe<BloomFruitChanged>(OnFruitChanged);
        }

        public WisdomTreeView View => _view;

        /// <summary>The tree bloomed in play and is waiting for its celebration to show it.</summary>
        public bool BloomPending { get; private set; }

        public void RevealBloom()
        {
            BloomPending = false;
            RefreshFruits();
            _view.SetBloom(true, true);
        }

        private void OnReady(QuizReady e) => _view.SetGlowing(true);

        private void OnAnswered(QuizAnswered e) => _view.SetGlowing(false);

        private void OnBloomStarted(BloomStarted e) => BloomPending = true;

        private void OnFruitChanged(BloomFruitChanged e)
        {
            for (int i = 0; i < _config.Fruits.Count; i++)
                if (_config.Fruits[i] != null && _config.Fruits[i].Id == e.FruitId) _view.SetFruitRipe(i, e.IsRipe);
        }

        private void RefreshFruits()
        {
            for (int i = 0; i < _config.Fruits.Count; i++)
                if (_config.Fruits[i] != null) _view.SetFruitRipe(i, _bloom.IsRipe(_config.Fruits[i].Id));
        }
    }
}
