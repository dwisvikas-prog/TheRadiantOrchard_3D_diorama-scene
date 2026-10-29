using System;
using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.App
{
    /// <summary>
    /// The services the Bootstrapper builds in Boot, handed to the GameInstaller in Game. Only the composition root
    /// reads Current; every other system gets what it needs passed in through its Init method.
    /// </summary>
    public sealed class AppServices
    {
        public AppServices(GameConfig config, TimeService time, GameClock clock, SaveService save, EventBus events, TimeSpan launchTimeAway)
        {
            Config = config;
            Time = time;
            Clock = clock;
            Save = save;
            Events = events;
            LaunchTimeAway = launchTimeAway;
        }

        public static AppServices Current { get; internal set; }

        public GameConfig Config { get; }
        public TimeService Time { get; }
        public GameClock Clock { get; }
        public SaveService Save { get; }
        public EventBus Events { get; }
        public GameState State => Save.State;

        /// <summary>Real time that passed while the app was closed (0 on first launch or if the clock was turned back).</summary>
        public TimeSpan LaunchTimeAway { get; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
