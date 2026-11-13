# Architecture

FlowClicker is a Windows x64 WPF app. Views and view models collect settings, and the click, sequence, and mouse utility services send mouse events through Win32.

- **Auto Clicker** runs on a cancellable worker. Follow cursor sends button events at the current pointer position. Fixed coordinates sends the move and button events together.
- **Multi-Point** takes a snapshot of the configured points and counts completed cycles. The UI polls progress while the sequence runs.
- **Mouse Tools** includes auto-scroll, pointer motion, and mouse-button hold. Each service releases the buttons it owns when it stops. Shift+Esc requests cancellation and releases mouse buttons.
- **Timing** uses a monotonic clock and skips missed click slots. Throughput depends on Windows scheduling, the input rate, and the target application.

## Windows design references

- [Fluent Design for Windows apps](https://learn.microsoft.com/windows/apps/design/)
- [Color](https://learn.microsoft.com/windows/apps/design/signature-experiences/color)
- [Geometry](https://learn.microsoft.com/windows/apps/design/signature-experiences/geometry)
- [Materials](https://learn.microsoft.com/windows/apps/design/signature-experiences/materials)
- [Typography](https://learn.microsoft.com/windows/apps/design/signature-experiences/typography)
