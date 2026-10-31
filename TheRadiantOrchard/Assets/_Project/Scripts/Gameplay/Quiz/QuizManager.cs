using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The Wisdom Tree quiz (CLAUDE.md 5.8). Heals count up in GameState.HealedSinceQuiz (HealService); at QuizEveryHeals
    /// a quiz is ready and QuizReady is announced. Open picks the question (QuizRules: no repeats until all are seen)
    /// and keeps it until it is answered, so closing the panel brings the same question back. Answering uses the quiz
    /// up either way (agreed: no retry): it is recorded in quizSeen, the count starts again, a right answer starts Golden
    /// Harvest (BoostService) and QuizAnswered is announced. Dispose to unsubscribe.
    /// </summary>
    public sealed class QuizManager : IDisposable
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly BoostService _boosts;
        private readonly Action _markDirty;
        private readonly Random _random;
        private QuizScenario _pending;
        private bool _pendingStartsNewRound;
        private bool _announcedReady;

        public QuizManager(GameState state, GameConfig config, EventBus events, BoostService boosts, Action markDirty, Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _boosts = boosts ?? throw new ArgumentNullException(nameof(boosts));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            if (HasQuestions)
            {
                // A save that is already past the count launches with the tree glowing (views read IsReady at start).
                _announcedReady = IsReady;
                _events.Subscribe<VisitorHealed>(OnHealed);
            }
            else Log.Warn("QuizManager: GameConfig has no quiz questions; the Wisdom Tree never asks.");
        }

        public void Dispose() => _events.Unsubscribe<VisitorHealed>(OnHealed);

        public bool HasQuestions => _config.Quiz != null && _config.Quiz.Scenarios.Count > 0;
        public bool IsReady => HasQuestions && QuizRules.IsReady(_config.Balance, _state);

        /// <summary>The question to show, the same one until it is answered; null when no quiz is ready.</summary>
        public QuizScenario Open()
        {
            if (!IsReady) return null;
            if (_pending == null)
                _pending = QuizRules.PickNext(_config.Quiz.Scenarios, _state.QuizSeen, _random, out _pendingStartsNewRound);
            return _pending;
        }

        /// <summary>
        /// Answers the open question with one of its options (index into QuizScenario.Options). Returns the outcome; a
        /// wrong one carries the hint key. Nothing happens when no question is open.
        /// </summary>
        public QuizOutcome Answer(int optionIndex)
        {
            QuizScenario scenario = _pending;
            if (scenario == null || !IsReady) return QuizOutcome.None;

            bool correct = QuizRules.IsCorrect(scenario, optionIndex);
            if (_pendingStartsNewRound) _state.QuizSeen.Clear();
            _state.QuizSeen.Add(scenario.Id);
            _state.HealedSinceQuiz = 0;
            _pending = null;
            _announcedReady = false;
            _markDirty();

            if (correct)
            {
                BalanceConfig b = _config.Balance;
                _boosts.Start(BoostService.GoldenHarvest, b.GoldenHarvestSeedMultiplier, b.GoldenHarvestSeconds);
            }
            _events.Publish(new QuizAnswered(scenario.Id, correct));
            Telemetry.Log("quiz_answered", scenario.Id + (correct ? ":right" : ":wrong"));
            return new QuizOutcome(true, correct, correct ? null : scenario.HintKey);
        }

        /// <summary>Dev tool: makes a quiz ready now.</summary>
        public void MakeReady()
        {
            if (!HasQuestions) return;
            _state.HealedSinceQuiz = Math.Max(_state.HealedSinceQuiz, _config.Balance.QuizEveryHeals);
            _markDirty();
            AnnounceIfReady();
        }

        private void OnHealed(VisitorHealed e) => AnnounceIfReady();

        private void AnnounceIfReady()
        {
            if (_announcedReady || !IsReady) return;
            _announcedReady = true;
            _events.Publish(new QuizReady(_state.HealedSinceQuiz));
        }
    }
}
