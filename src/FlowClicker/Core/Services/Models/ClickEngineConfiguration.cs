using System;

namespace FlowClicker.Core.Services.Models
{
    public enum ClickMouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }

    public enum ClickType
    {
        Single,
        Double,
        Hold
    }

    public enum ClickLocationMode
    {
        CurrentCursor,
        FixedCoordinates
    }

    public enum TimerEngineMode
    {
        Precision, // QPC SpinWait + high-res waitable timer
        Eco        // Yield/Sleep, ultra-low CPU on mobile laptops
    }

    public enum InputFailureReason
    {
        None,
        ButtonOwnedByOther,
        InjectionBlocked
    }

    public sealed class ClickEngineConfiguration
    {
        public const double DefaultCps = 10;
        public const double MinIntervalMs = 0.5;
        public const double MaxIntervalMs = 86_400_000.0;

        public double Cps { get; set; } = DefaultCps;

        /// <summary>
        /// Authoritative interval in milliseconds. When null, derived from <see cref="Cps"/>.
        /// </summary>
        public double? IntervalMs { get; set; }

        public ClickMouseButton Button { get; set; } = ClickMouseButton.Left;
        public ClickType ClickType { get; set; } = ClickType.Single;
        public ClickLocationMode LocationMode { get; set; } = ClickLocationMode.CurrentCursor;
        public int FixedX { get; set; }
        public int FixedY { get; set; }
        public bool EnableHumanizer { get; set; } = false;
        public double HumanizerJitterMs { get; set; } = 5.0;
        public int HumanizerRadiusPx { get; set; } = 2;
        public long MaxClickCount { get; set; } = 0; // 0 = Infinite
        public TimerEngineMode EngineMode { get; set; } = TimerEngineMode.Eco;

        public double ResolveIntervalMs()
        {
            double raw = IntervalMs ?? (1000.0 / Math.Clamp(Cps, 0.00001, 2000.0));
            return Math.Clamp(raw, MinIntervalMs, MaxIntervalMs);
        }
    }
}
