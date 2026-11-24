using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FlowClicker.Core.Native
{
    public static class CoordinateConverter
    {
        /// <summary>
        /// Converts physical screen coordinates (desktop pixels) into normalized absolute coordinates [0, 65535]
        /// across the entire multi-monitor virtual desktop bounds.
        /// </summary>
        public static (int normX, int normY) ToNormalizedCoordinates(int physicalX, int physicalY)
        {
            int vLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int vTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int vWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int vHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

            return NormalizeToBounds(physicalX, physicalY, vLeft, vTop, vWidth, vHeight);
        }

        internal static (int normX, int normY) NormalizeToBounds(int physicalX, int physicalY, int vLeft, int vTop, int vWidth, int vHeight)
        {
            if (vWidth <= 0) vWidth = 1920;
            if (vHeight <= 0) vHeight = 1080;

            double clampedX = Math.Clamp((double)physicalX - vLeft, 0, vWidth - 1);
            double clampedY = Math.Clamp((double)physicalY - vTop, 0, vHeight - 1);

            int normX = Math.Clamp((int)Math.Floor((clampedX + 0.5) * 65536.0 / vWidth), 0, 65535);
            int normY = Math.Clamp((int)Math.Floor((clampedY + 0.5) * 65536.0 / vHeight), 0, 65535);

            return (normX, normY);
        }

        /// <summary>
        /// Enumerates physical bounding rectangles for all connected monitors.
        /// </summary>
        public static IReadOnlyList<RECT> GetMonitorBounds()
        {
            var monitors = new List<RECT>();
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
                {
                    var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (NativeMethods.GetMonitorInfo(hMonitor, ref info) && info.rcMonitor.Width > 0 && info.rcMonitor.Height > 0)
                    {
                        monitors.Add(info.rcMonitor);
                    }
                    return true;
                }, IntPtr.Zero);

            if (monitors.Count == 0)
            {
                int left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
                int top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
                int width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN));
                int height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
                monitors.Add(new RECT { Left = left, Top = top, Right = left + width, Bottom = top + height });
            }

            return monitors;
        }

        /// <summary>
        /// Gets current physical cursor coordinates using Win32 GetCursorPos.
        /// </summary>
        public static (int x, int y) GetCurrentCursorPosition()
        {
            if (NativeMethods.GetCursorPos(out POINT pt))
            {
                return (pt.X, pt.Y);
            }
            return (0, 0);
        }
    }
}
