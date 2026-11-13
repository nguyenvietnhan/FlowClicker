using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowClicker.Core.Services;
using FlowClicker.Core.Services.Models;
using FlowClicker.UI.Views;

namespace FlowClicker.UI.ViewModels
{
    public partial class MultiPointViewModel : ObservableObject, IDisposable
    {
        private readonly ISequenceEngine _sequenceEngine;
        private readonly DispatcherTimer _progressTimer;

        [ObservableProperty]
        private ObservableCollection<SequenceActionPoint> _points = new();

        [ObservableProperty]
        private SequenceActionPoint? _selectedPoint;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private int _currentStepIndex;

        [ObservableProperty]
        private int _completedCycles;

        [ObservableProperty]
        private int _repeatCount = 0; // 0 = infinite

        [ObservableProperty]
        private bool _inputFailed;

        public MultiPointViewModel(ISequenceEngine sequenceEngine)
        {
            _sequenceEngine = sequenceEngine;
            _sequenceEngine.StateChanged += OnStateChanged;
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _progressTimer.Tick += UpdateProgress;

            // Seed sample default sequence
            Points.Add(new SequenceActionPoint { Order = 1, X = 500, Y = 400, PreDelayMs = 100, PostDelayMs = 200, Comment = "Target Button" });
            Points.Add(new SequenceActionPoint { Order = 2, X = 600, Y = 500, PreDelayMs = 150, PostDelayMs = 300, Comment = "Confirm Dialog" });
        }

        private void OnStateChanged(object? sender, bool running)
        {
            IsRunning = _sequenceEngine.IsRunning;
            InputFailed = !IsRunning && _sequenceEngine.InputFailed;
            if (IsRunning)
                _progressTimer.Start();
            else
                _progressTimer.Stop();

            UpdateProgress(null, EventArgs.Empty);
        }

        private void UpdateProgress(object? sender, EventArgs e)
        {
            CurrentStepIndex = _sequenceEngine.CurrentStepIndex;
            CompletedCycles = _sequenceEngine.CompletedCycles;
        }

        public void Dispose()
        {
            _progressTimer.Stop();
            _sequenceEngine.StateChanged -= OnStateChanged;
        }

        [RelayCommand]
        public void AddPoint()
        {
            int order = Points.Count + 1;
            var pt = new SequenceActionPoint
            {
                Order = order,
                X = 200 + (order * 50),
                Y = 200 + (order * 30),
                PreDelayMs = 50,
                PostDelayMs = 150,
                Comment = $"Step {order}"
            };
            Points.Add(pt);
            SelectedPoint = pt;
        }

        [RelayCommand]
        public void PickAndAddPoint()
        {
            var picker = new CoordinatePickerWindow();
            picker.CoordinateSelected += (s, pt) =>
            {
                int order = Points.Count + 1;
                var actionPoint = new SequenceActionPoint
                {
                    Order = order,
                    X = pt.X,
                    Y = pt.Y,
                    PreDelayMs = 50,
                    PostDelayMs = 150,
                    Comment = $"Picked Point {order}"
                };
                Points.Add(actionPoint);
                SelectedPoint = actionPoint;
            };
            picker.ShowDialog();
        }

        [RelayCommand]
        public void RemovePoint()
        {
            if (SelectedPoint != null)
            {
                Points.Remove(SelectedPoint);
                ReorderPoints();
            }
        }

        [RelayCommand]
        public void MoveUp()
        {
            if (SelectedPoint == null) return;
            int idx = Points.IndexOf(SelectedPoint);
            if (idx > 0)
            {
                Points.Move(idx, idx - 1);
                ReorderPoints();
            }
        }

        [RelayCommand]
        public void MoveDown()
        {
            if (SelectedPoint == null) return;
            int idx = Points.IndexOf(SelectedPoint);
            if (idx >= 0 && idx < Points.Count - 1)
            {
                Points.Move(idx, idx + 1);
                ReorderPoints();
            }
        }

        [RelayCommand]
        public void Clear()
        {
            Points.Clear();
        }

        [RelayCommand]
        public void ToggleSequence()
        {
            if (_sequenceEngine.IsRunning)
            {
                _sequenceEngine.Stop();
            }
            else
            {
                if (Points.Count == 0) return;
                _sequenceEngine.Start(Points, RepeatCount);
            }
        }

        private void ReorderPoints()
        {
            for (int i = 0; i < Points.Count; i++)
            {
                Points[i].Order = i + 1;
            }
        }
    }
}
