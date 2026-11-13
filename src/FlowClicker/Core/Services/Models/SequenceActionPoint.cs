using System;

namespace FlowClicker.Core.Services.Models
{
    public sealed class SequenceActionPoint
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public int Order { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public ClickMouseButton Button { get; set; } = ClickMouseButton.Left;
        public ClickType Type { get; set; } = ClickType.Single;
        public int PreDelayMs { get; set; } = 50;
        public int PostDelayMs { get; set; } = 100;
        public string Comment { get; set; } = string.Empty;

        public SequenceActionPoint Clone()
        {
            return new SequenceActionPoint
            {
                Id = Guid.NewGuid(),
                Order = Order,
                X = X,
                Y = Y,
                Button = Button,
                Type = Type,
                PreDelayMs = PreDelayMs,
                PostDelayMs = PostDelayMs,
                Comment = Comment
            };
        }
    }
}
