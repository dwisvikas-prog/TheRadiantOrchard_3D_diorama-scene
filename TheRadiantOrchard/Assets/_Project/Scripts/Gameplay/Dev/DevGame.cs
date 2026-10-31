using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The running game's systems, for dev tools only (the in-game dev panel and the Editor's Dev menu; ENGINEERING_GUIDE
    /// 9.2). The GameInstaller fills Current in the Editor and development builds and clears it when the Game scene
    /// unloads; release builds never set it. Like Core.DevHooks, it lets the Dev assembly reach the game while no
    /// runtime assembly references Dev.
    /// </summary>
    public sealed class DevGame
    {
        public DevGame(GameConfig config, GameState state, TimeService time, EventBus events, SaveService save, PlotManager plots, HealService heal, VisitorManager visitors, DeliveryService delivery, BoostService boosts, VibrancyManager vibrancy, QuizManager quiz, DailyWave dailyWave)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Save = save ?? throw new ArgumentNullException(nameof(save));
            Plots = plots ?? throw new ArgumentNullException(nameof(plots));
            Heal = heal ?? throw new ArgumentNullException(nameof(heal));
            Visitors = visitors ?? throw new ArgumentNullException(nameof(visitors));
            Delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
            Boosts = boosts ?? throw new ArgumentNullException(nameof(boosts));
            Vibrancy = vibrancy ?? throw new ArgumentNullException(nameof(vibrancy));
            Quiz = quiz ?? throw new ArgumentNullException(nameof(quiz));
            DailyWave = dailyWave ?? throw new ArgumentNullException(nameof(dailyWave));
        }

        /// <summary>The running game, or null outside the Game scene and in release builds.</summary>
        public static DevGame Current { get; set; }

        public GameConfig Config { get; }
        public GameState State { get; }
        public TimeService Time { get; }
        public EventBus Events { get; }
        public SaveService Save { get; }
        public PlotManager Plots { get; }
        public HealService Heal { get; }
        public DeliveryService Delivery { get; }
        public BoostService Boosts { get; }
        public VisitorManager Visitors { get; }
        public VibrancyManager Vibrancy { get; }
        public QuizManager Quiz { get; }
        public DailyWave DailyWave { get; }
    }
}
