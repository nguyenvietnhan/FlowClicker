using System;

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

            if (vWidth <= 1) vWidth = 1920;
            if (vHeight <= 1) vHeight = 1080;

            int normX = (int)Math.Round(Math.Clamp((physicalX - (double)vLeft) / (vWidth - 1), 0, 1) * 65535);
            int normY = (int)Math.Round(Math.Clamp((physicalY - (double)vTop) / (vHeight - 1), 0, 1) * 65535);

            return (normX, normY);
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
