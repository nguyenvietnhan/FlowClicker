using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowClicker.Core.Services.Models
{
    public sealed partial class SequenceActionPoint : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        [ObservableProperty]
        private int _order;

        [ObservableProperty]
        private int _x;

        [ObservableProperty]
        private int _y;

        [ObservableProperty]
        private ClickMouseButton _button = ClickMouseButton.Left;

        [ObservableProperty]
        private ClickType _type = ClickType.Single;

        [ObservableProperty]
        private int _preDelayMs = 50;

        [ObservableProperty]
        private int _postDelayMs = 100;

        [ObservableProperty]
        private string _comment = string.Empty;

        partial void OnPreDelayMsChanged(int value)
        {
            if (value < 0)
                PreDelayMs = 0;
        }

        partial void OnPostDelayMsChanged(int value)
        {
            if (value < 0)
                PostDelayMs = 0;
        }

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
                PreDelayMs = Math.Max(0, PreDelayMs),
                PostDelayMs = Math.Max(0, PostDelayMs),
                Comment = Comment
            };
        }
    }
}
