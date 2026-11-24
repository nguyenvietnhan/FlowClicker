# Architecture

FlowClicker is a Windows x64 WPF app. Views and view models collect and persist settings in `%LocalAppData%\FlowClicker\settings.json`, while the click, sequence, and mouse utility services send mouse events through Win32.

- **Auto Clicker** runs on a cancellable worker. Follow cursor sends button events at the current pointer position. Fixed coordinates sends the move and button events together. Hold mode presses the button once and keeps it held until cancelled. Timing supports lossless millisecond intervals (`0.5 ms` up to 24 hours), optional click count limits, and `Eco`/`Precision` wait modes.
- **Multi-Point** takes a snapshot of the configured points and counts completed cycles. Editing is locked while running, and the UI polls progress (`1`-based step and cycle counts).
- **Coordinate Picker** spawns a per-monitor `PerMonitorV2` overlay across all connected displays and prevents duplicate modal overlays when `F7` is pressed.
- **Mouse Tools** includes auto-scroll, zero-sum pointer jiggler, and mouse-button hold. Each service owns its button locks and surfaces injection or contention errors to the UI. `Shift+Esc` requests cancellation and releases mouse buttons; unhandled exceptions also invoke emergency button release.
- **Timing** uses a monotonic clock, skips missed click slots, and combines `WaitHandle` / high-resolution waitable timers (`CREATE_WAITABLE_TIMER_HIGH_RESOLUTION`) / QPC `SpinWait`.

## Windows design references

- [Fluent Design for Windows apps](https://learn.microsoft.com/windows/apps/design/)
- [Color](https://learn.microsoft.com/windows/apps/design/signature-experiences/color)
- [Geometry](https://learn.microsoft.com/windows/apps/design/signature-experiences/geometry)
- [Materials](https://learn.microsoft.com/windows/apps/design/signature-experiences/materials)
- [Typography](https://learn.microsoft.com/windows/apps/design/signature-experiences/typography)
