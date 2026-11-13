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
        Precision, // QPC SpinWait, deterministic <= 10us accuracy
        Eco        // Yield/Sleep, ultra-low CPU on mobile laptops
    }

    public sealed class ClickEngineConfiguration
    {
        public const double DefaultCps = 10;
        public double Cps { get; set; } = DefaultCps;
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
    }
}
