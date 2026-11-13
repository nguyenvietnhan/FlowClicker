using System;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Engine
{
    public interface IClickEngine
    {
        bool IsRunning { get; }
        bool InputFailed { get; }
        long TotalClicks { get; }
        void Start(ClickEngineConfiguration config);
        void Stop();
        event EventHandler<bool>? StateChanged;
    }
}
