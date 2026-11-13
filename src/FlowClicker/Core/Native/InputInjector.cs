using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FlowClicker.Core.Native
{
    public static class InputInjector
    {
        private static readonly int InputStructSize = Marshal.SizeOf<INPUT>();
        private static long _failedInjections;

        public static long FailedInjections => Interlocked.Read(ref _failedInjections);

        /// <summary>
        /// Injects an array of INPUT structures into the Windows OS input stream.
        /// </summary>
        public static uint Inject(INPUT[] inputs)
        {
            if (inputs == null || inputs.Length == 0) return 0;

            return Inject(inputs, inputs.Length);
        }

        internal static uint Inject(INPUT[] inputs, int count)
        {
            uint sent = NativeMethods.SendInput((uint)count, inputs, InputStructSize);
            if (sent != count)
            {
                Interlocked.Increment(ref _failedInjections);
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 0)
                {
                    Debug.WriteLine($"[InputInjector] SendInput partial/failed: Injected {sent} of {count}. Error: {errorCode}");
                }
            }
            return sent;
        }

        /// <summary>
        /// Requests release of every mouse button and returns the number of events Windows accepted.
        /// </summary>
        public static uint ReleaseAllMouseButtonLocks()
        {
            var cleanupInputs = new INPUT[]
            {
                INPUT.CreateMouseInput(new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP }),
                INPUT.CreateMouseInput(new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP }),
                INPUT.CreateMouseInput(new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_MIDDLEUP }),
                INPUT.CreateMouseInput(new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_XUP, mouseData = (uint)NativeMethods.XBUTTON1 }),
                INPUT.CreateMouseInput(new MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_XUP, mouseData = (uint)NativeMethods.XBUTTON2 })
            };
            return Inject(cleanupInputs);
        }
    }
}
