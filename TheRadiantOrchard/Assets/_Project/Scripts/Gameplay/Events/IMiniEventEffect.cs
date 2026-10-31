using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// What one kind of mini-event does to the game (CLAUDE.md 5.11). MiniEventManager picks an event and hands it to
    /// the effect for its kind; a new kind (Phase 2's "challenge") is a new effect class plus an asset.
    /// </summary>
    public interface IMiniEventEffect
    {
        MiniEventKind Kind { get; }

        /// <summary>Whether starting this event now would do anything (e.g. a rush needs free path spots).</summary>
        bool CanStart(MiniEventDefinition definition);

        void Start(MiniEventDefinition definition);
    }
}
