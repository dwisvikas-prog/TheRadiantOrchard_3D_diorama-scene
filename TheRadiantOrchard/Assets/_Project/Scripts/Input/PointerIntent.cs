namespace RadiantOrchard.Input
{
    /// <summary>What the current one-finger press has turned out to be (GestureRules).</summary>
    public enum PointerIntent
    {
        /// <summary>No press in progress.</summary>
        None = 0,
        /// <summary>Pressed, still short and still: may become a tap, long press, swipe or pan.</summary>
        Undecided = 1,
        /// <summary>Moved past the drag threshold slowly: the camera pans; no gesture can fire.</summary>
        Pan = 2,
        /// <summary>Moved past the drag threshold fast: a swipe; the camera stays still.</summary>
        Swipe = 3,
        /// <summary>Long press already fired; the rest of the press does nothing.</summary>
        Held = 4,
        /// <summary>Started on the UI or cancelled by a second finger; the rest of the press does nothing.</summary>
        Ignored = 5,
    }
}
