using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;

namespace RadiantOrchard.Presentation
{
    /// <summary>Lights the Wisdom Tree while a quiz is waiting and puts it out once answered. Dispose to unsubscribe.</summary>
    public sealed class WisdomTreePresenter : IDisposable
    {
        private readonly WisdomTreeView _view;
        private readonly EventBus _events;

        public WisdomTreePresenter(WisdomTreeView view, QuizManager quiz, EventBus events)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _view.SetGlowing(quiz != null && quiz.IsReady);
            _events.Subscribe<QuizReady>(OnReady);
            _events.Subscribe<QuizAnswered>(OnAnswered);
        }

        public void Dispose()
        {
            _events.Unsubscribe<QuizReady>(OnReady);
            _events.Unsubscribe<QuizAnswered>(OnAnswered);
        }

        private void OnReady(QuizReady e) => _view.SetGlowing(true);

        private void OnAnswered(QuizAnswered e) => _view.SetGlowing(false);
    }
}
