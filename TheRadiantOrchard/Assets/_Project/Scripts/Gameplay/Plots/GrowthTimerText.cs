using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Text for a growth countdown: under an hour as minutes:seconds ("4:59"), otherwise hours and minutes ("1h 05m").
    /// Both patterns come from the StringTable so wording can change without code.
    /// </summary>
    public static class GrowthTimerText
    {
        public const string MinutesKey = "ui.timer.minutes";
        public const string HoursKey = "ui.timer.hours";

        public static string Format(int secondsLeft, StringTable strings)
        {
            if (secondsLeft < 0) secondsLeft = 0;
            if (secondsLeft < 3600)
                return string.Format(strings.Get(MinutesKey), secondsLeft / 60, secondsLeft % 60);
            return string.Format(strings.Get(HoursKey), secondsLeft / 3600, secondsLeft % 3600 / 60);
        }
    }
}
