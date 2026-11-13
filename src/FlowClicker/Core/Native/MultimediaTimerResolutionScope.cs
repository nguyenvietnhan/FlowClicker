using System;
using System.Diagnostics;

namespace FlowClicker.Core.Native
{
    /// <summary>
    /// RAII scope that elevates the Windows kernel timer resolution to 1ms
    /// and guarantees restoration of default timer resolution on Dispose.
    /// </summary>
    public sealed class MultimediaTimerResolutionScope : IDisposable
    {
        private bool _disposed;
        private readonly uint _periodMs;

        public MultimediaTimerResolutionScope(uint periodMs = 1)
        {
            _periodMs = periodMs;
            uint result = NativeMethods.TimeBeginPeriod(_periodMs);
            if (result != NativeMethods.TIMERR_NOERROR)
            {
                Debug.WriteLine($"[MultimediaTimerResolutionScope] Failed to set timer period to {_periodMs}ms (Code: {result})");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                NativeMethods.TimeEndPeriod(_periodMs);
                _disposed = true;
            }
        }
    }
}
