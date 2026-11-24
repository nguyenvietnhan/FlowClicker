using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Services
{
    public sealed class AppSettingsData
    {
        public string SelectedTheme { get; set; } = "System";
        public bool IsAlwaysOnTop { get; set; }

        public AutoClickerSettingsData AutoClicker { get; set; } = new();
        public MultiPointSettingsData MultiPoint { get; set; } = new();
        public MouseToolsSettingsData MouseTools { get; set; } = new();
    }

    public sealed class AutoClickerSettingsData
    {
        public double IntervalMs { get; set; } = 1000.0 / ClickEngineConfiguration.DefaultCps;
        public ClickMouseButton Button { get; set; } = ClickMouseButton.Left;
        public ClickType ClickType { get; set; } = ClickType.Single;
        public bool IsCurrentCursor { get; set; } = true;
        public int FixedX { get; set; } = 100;
        public int FixedY { get; set; } = 100;
        public bool EnableHumanizer { get; set; }
        public double HumanizerJitterMs { get; set; } = 5.0;
        public int HumanizerRadiusPx { get; set; } = 2;
        public bool IsInfiniteRepeat { get; set; } = true;
        public long RepeatTimes { get; set; } = 100;
        public TimerEngineMode EngineMode { get; set; } = TimerEngineMode.Eco;
    }

    public sealed class MultiPointSettingsData
    {
        public int RepeatCount { get; set; }
        public List<SequencePointData>? Points { get; set; }
    }

    public sealed class SequencePointData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public ClickMouseButton Button { get; set; } = ClickMouseButton.Left;
        public ClickType Type { get; set; } = ClickType.Single;
        public int PreDelayMs { get; set; } = 50;
        public int PostDelayMs { get; set; } = 100;
        public string Comment { get; set; } = string.Empty;
    }

    public sealed class MouseToolsSettingsData
    {
        public int ScrollSpeed { get; set; } = 20;
        public ScrollDirection ScrollDirection { get; set; } = ScrollDirection.Down;
        public int JigglerInterval { get; set; } = 10;
        public JigglerMode JigglerMode { get; set; } = JigglerMode.Lissajous;
        public ClickMouseButton HoldButton { get; set; } = ClickMouseButton.Left;
    }

    public sealed class SettingsPersistenceService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _settingsPath;
        private readonly object _ioLock = new();
        private readonly Timer _saveDebounceTimer;
        private bool _disposed;

        public AppSettingsData Current { get; private set; }

        public SettingsPersistenceService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowClicker",
                "settings.json"))
        {
        }

        public SettingsPersistenceService(string settingsPath)
        {
            _settingsPath = settingsPath;
            _saveDebounceTimer = new Timer(_ => SaveNow(), null, Timeout.Infinite, Timeout.Infinite);
            Current = Load();
        }

        public AppSettingsData Load()
        {
            lock (_ioLock)
            {
                if (!File.Exists(_settingsPath))
                {
                    Current = new AppSettingsData();
                    return Current;
                }

                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    Current = JsonSerializer.Deserialize<AppSettingsData>(json, JsonOptions) ?? new AppSettingsData();
                    return Current;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SettingsPersistenceService] Failed to parse settings: {ex.Message}");
                    try
                    {
                        File.Copy(_settingsPath, _settingsPath + ".corrupt", overwrite: true);
                    }
                    catch
                    {
                        // Best-effort backup only.
                    }
                    Current = new AppSettingsData();
                    return Current;
                }
            }
        }

        public void ScheduleSave()
        {
            lock (_ioLock)
            {
                if (_disposed) return;
                _saveDebounceTimer.Change(300, Timeout.Infinite);
            }
        }

        public void SaveNow() => Save(Current);

        public void Save(AppSettingsData data)
        {
            lock (_ioLock)
            {
                try
                {
                    string? dir = Path.GetDirectoryName(_settingsPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    string tmpPath = _settingsPath + ".tmp";
                    string json = JsonSerializer.Serialize(data, JsonOptions);
                    File.WriteAllText(tmpPath, json);
                    File.Move(tmpPath, _settingsPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SettingsPersistenceService] Failed to save settings: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            lock (_ioLock)
            {
                if (_disposed) return;
                _disposed = true;
                _saveDebounceTimer.Dispose();
            }
            Save(Current);
        }
    }

    public static class InputFailureMessages
    {
        public static string Describe(InputFailureReason reason) => reason switch
        {
            InputFailureReason.ButtonOwnedByOther =>
                "That mouse button is currently held by another FlowClicker tool. Press Shift+Esc to release held buttons.",
            InputFailureReason.InjectionBlocked =>
                "Windows blocked synthetic input. If the target window runs as administrator, restart FlowClicker as administrator in Settings.",
            _ => string.Empty
        };
    }
}
