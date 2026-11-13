using System;
using System.Collections.Generic;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Services
{
    public interface ISequenceEngine
    {
        bool IsRunning { get; }
        bool InputFailed { get; }
        int CurrentStepIndex { get; }
        int CompletedCycles { get; }
        void Start(IReadOnlyList<SequenceActionPoint> points, int repeatCount = 0);
        void Stop();
        event EventHandler<bool>? StateChanged;
    }
}
