using System;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Services
{
    public enum ScrollDirection
    {
        Down,
        Up,
        Right,
        Left
    }

    public enum JigglerMode
    {
        MicroNudge,  // 1px left/right
        Lissajous    // Smooth subtle zero-sum curve
    }

    public interface IMouseUtilityService
    {
        bool IsAutoScrollRunning { get; }
        bool IsJigglerRunning { get; }
        bool IsHoldLockActive { get; }
        bool InputFailed { get; }
        InputFailureReason FailureReason { get; }

        void StartAutoScroll(ScrollDirection direction, int speedStepsPerSecond);
        void StopAutoScroll();

        void StartJiggler(int intervalSeconds = 15, JigglerMode mode = JigglerMode.Lissajous);
        void StopJiggler();

        void ToggleHoldLock(bool leftButton = true);
        void ToggleHoldLock(ClickMouseButton button);
        void ReleaseHoldLock();

        event EventHandler<bool>? AutoScrollStateChanged;
        event EventHandler<bool>? JigglerStateChanged;
        event EventHandler<bool>? HoldLockStateChanged;
        event EventHandler? FailureStateChanged;
    }
}
