using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FlowClicker.Core.Native;

namespace FlowClicker.Core.Services
{
    public sealed class HotkeyManager : IDisposable
    {
        private HwndSource? _hwndSource;
        private readonly Dictionary<int, Action> _hotkeyActions = new();
        private int _currentId;
        private bool _isInitialized;

        public void Initialize(Window window)
        {
            if (_isInitialized) return;

            var helper = new WindowInteropHelper(window);
            IntPtr handle = helper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(HwndHook);
            _isInitialized = true;
        }

        public int Register(uint modifiers, Key key, Action callback)
        {
            if (_hwndSource == null)
            {
                throw new InvalidOperationException("HotkeyManager must be initialized with a Window before registering hotkeys.");
            }

            int id = Interlocked.Increment(ref _currentId);
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

            // MOD_NOREPEAT (0x4000) prevents repeated WM_HOTKEY messages on key hold
            bool success = NativeMethods.RegisterHotKey(_hwndSource.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[HotkeyManager] RegisterHotKey failed for Key {key} (Win32 Error: {error})");
                return -1;
            }

            lock (_hotkeyActions)
            {
                _hotkeyActions[id] = callback;
            }
            return id;
        }

        public void Unregister(int id)
        {
            if (id <= 0 || _hwndSource == null || _hwndSource.IsDisposed) return;

            NativeMethods.UnregisterHotKey(_hwndSource.Handle, id);
            lock (_hotkeyActions)
            {
                _hotkeyActions.Remove(id);
            }
        }

        public void UnregisterAll()
        {
            if (_hwndSource != null && !_hwndSource.IsDisposed)
            {
                lock (_hotkeyActions)
                {
                    foreach (int id in _hotkeyActions.Keys)
                    {
                        NativeMethods.UnregisterHotKey(_hwndSource.Handle, id);
                    }
                    _hotkeyActions.Clear();
                }
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                Action? callback = null;

                lock (_hotkeyActions)
                {
                    _hotkeyActions.TryGetValue(id, out callback);
                }

                if (callback != null)
                {
                    callback.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAll();
            if (_hwndSource != null && !_hwndSource.IsDisposed)
            {
                _hwndSource.RemoveHook(HwndHook);
            }
            _isInitialized = false;
        }
    }
}
