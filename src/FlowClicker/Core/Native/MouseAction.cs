using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Native;

internal sealed class MouseAction
{
    private static readonly object Gate = new();
    private static readonly MouseAction?[] Owners = new MouseAction[5];
    private readonly INPUT[] _buffer = new INPUT[5];
    private readonly Func<INPUT[], int, uint> _inject;

    public MouseAction() : this(InputInjector.Inject) { }

    internal MouseAction(Func<INPUT[], int, uint> inject) => _inject = inject;

    public bool Send(ClickMouseButton button, ClickType type, (int X, int Y)? position = null,
        CancellationToken cancellationToken = default)
    {
        int index = (int)button;
        if ((uint)index >= Owners.Length || (uint)type > (uint)ClickType.Hold)
            return false;

        var (down, up, data) = GetFlags(button);
        int count = 0;
        if (position is { } point)
        {
            var (x, y) = CoordinateConverter.ToNormalizedCoordinates(point.X, point.Y);
            _buffer[count++] = INPUT.CreateMouseInput(new MOUSEINPUT
            {
                dx = x, dy = y,
                dwFlags = NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_VIRTUALDESK | NativeMethods.MOUSEEVENTF_MOVE
            });
        }

        int movementCount = count;
        _buffer[count++] = ButtonInput(down, data);
        if (type != ClickType.Hold)
            _buffer[count++] = ButtonInput(up, data);
        if (type == ClickType.Double)
        {
            _buffer[count++] = ButtonInput(down, data);
            _buffer[count++] = ButtonInput(up, data);
        }

        lock (Gate)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (Owners[index] is { } owner && owner != this)
                return false;

            uint sent = _inject(_buffer, count);
            if (sent > movementCount)
                Owners[index] = (sent - movementCount) % 2 == 1 ? this : null;
            return sent == count;
        }
    }

    public static void EmergencyRelease()
    {
        lock (Gate)
        {
            if (InputInjector.ReleaseAllMouseButtonLocks() == Owners.Length)
                Array.Clear(Owners);
        }
    }

    public bool Release()
    {
        lock (Gate)
        {
            bool released = true;
            for (int index = 0; index < Owners.Length; index++)
            {
                if (Owners[index] != this)
                    continue;

                var (_, up, data) = GetFlags((ClickMouseButton)index);
                _buffer[0] = ButtonInput(up, data);
                if (_inject(_buffer, 1) == 1)
                    Owners[index] = null;
                else
                    released = false;
            }
            return released;
        }
    }

    private static INPUT ButtonInput(uint flags, uint data) =>
        INPUT.CreateMouseInput(new MOUSEINPUT { dwFlags = flags, mouseData = data });

    private static (uint Down, uint Up, uint Data) GetFlags(ClickMouseButton button) => button switch
    {
        ClickMouseButton.Right => (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP, 0),
        ClickMouseButton.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP, 0),
        ClickMouseButton.XButton1 => (NativeMethods.MOUSEEVENTF_XDOWN, NativeMethods.MOUSEEVENTF_XUP, NativeMethods.XBUTTON1),
        ClickMouseButton.XButton2 => (NativeMethods.MOUSEEVENTF_XDOWN, NativeMethods.MOUSEEVENTF_XUP, NativeMethods.XBUTTON2),
        _ => (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP, 0)
    };
}
