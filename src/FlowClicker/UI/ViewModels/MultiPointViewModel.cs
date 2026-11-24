using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
        private readonly SettingsPersistenceService? _persistence;
        private readonly DispatcherTimer _progressTimer;
        private bool _isInitializing;

        [ObservableProperty]
        private ObservableCollection<SequenceActionPoint> _points = new();

        [ObservableProperty]
        private SequenceActionPoint? _selectedPoint;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayStep))]
        private bool _isRunning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayStep))]
        private int _currentStepIndex;

        [ObservableProperty]
        private int _completedCycles;

        [ObservableProperty]
        private int _repeatCount = 0; // 0 = infinite

        [ObservableProperty]
        private bool _inputFailed;

        [ObservableProperty]
        private string _failureMessage = string.Empty;

        public string DisplayStep => IsRunning && Points.Count > 0
            ? $"{Math.Clamp(CurrentStepIndex + 1, 1, Points.Count)} / {Points.Count}"
            : "—";

        public MultiPointViewModel(ISequenceEngine sequenceEngine, SettingsPersistenceService? persistence = null)
        {
            _sequenceEngine = sequenceEngine;
            _persistence = persistence;
            _sequenceEngine.StateChanged += OnStateChanged;
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _progressTimer.Tick += UpdateProgress;

            Points.CollectionChanged += OnPointsCollectionChanged;
            LoadFromPersistence();
        }

        private void LoadFromPersistence()
        {
            _isInitializing = true;
            try
            {
                var saved = _persistence?.Current.MultiPoint;
                if (saved?.Points != null)
                {
                    RepeatCount = Math.Max(0, saved.RepeatCount);
                    foreach (var p in saved.Points)
                    {
                        Points.Add(new SequenceActionPoint
                        {
                            X = p.X,
                            Y = p.Y,
                            Button = p.Button,
                            Type = p.Type,
                            PreDelayMs = Math.Max(0, p.PreDelayMs),
                            PostDelayMs = Math.Max(0, p.PostDelayMs),
                            Comment = p.Comment
                        });
                    }
                    ReorderPoints();
                }
                else
                {
                    Points.Add(new SequenceActionPoint { Order = 1, X = 500, Y = 400, PreDelayMs = 100, PostDelayMs = 200, Comment = "Target Button" });
                    Points.Add(new SequenceActionPoint { Order = 2, X = 600, Y = 500, PreDelayMs = 150, PostDelayMs = 300, Comment = "Confirm Dialog" });
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SequenceActionPoint item in e.OldItems)
                    item.PropertyChanged -= OnPointPropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (SequenceActionPoint item in e.NewItems)
                    item.PropertyChanged += OnPointPropertyChanged;
            }
            OnPropertyChanged(nameof(DisplayStep));
            Persist();
        }

        private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SequenceActionPoint.Order))
                Persist();
        }

        partial void OnRepeatCountChanged(int value)
        {
            if (value < 0)
                RepeatCount = 0;
            Persist();
        }

        private void Persist()
        {
            if (_isInitializing || _persistence == null) return;
            _persistence.Current.MultiPoint.RepeatCount = Math.Max(0, RepeatCount);
            _persistence.Current.MultiPoint.Points = Points.Select(p => new SequencePointData
            {
                X = p.X,
                Y = p.Y,
                Button = p.Button,
                Type = p.Type,
                PreDelayMs = p.PreDelayMs,
                PostDelayMs = p.PostDelayMs,
                Comment = p.Comment
            }).ToList();
            _persistence.ScheduleSave();
        }

        private void OnStateChanged(object? sender, bool running)
        {
            IsRunning = _sequenceEngine.IsRunning;
            InputFailed = !IsRunning && _sequenceEngine.InputFailed;
            FailureMessage = InputFailed ? InputFailureMessages.Describe(_sequenceEngine.FailureReason) : string.Empty;
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
            Points.CollectionChanged -= OnPointsCollectionChanged;
            foreach (var item in Points)
                item.PropertyChanged -= OnPointPropertyChanged;
        }

        [RelayCommand]
        public void AddPoint()
        {
            if (_sequenceEngine.IsRunning) return;
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
            if (_sequenceEngine.IsRunning) return;
            CoordinatePickerWindow.ShowPicker(pt =>
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
            });
        }

        [RelayCommand]
        public void RemovePoint()
        {
            if (_sequenceEngine.IsRunning || SelectedPoint == null) return;
            Points.Remove(SelectedPoint);
            ReorderPoints();
        }

        [RelayCommand]
        public void MoveUp()
        {
            if (_sequenceEngine.IsRunning || SelectedPoint == null) return;
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
            if (_sequenceEngine.IsRunning || SelectedPoint == null) return;
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
            if (_sequenceEngine.IsRunning) return;
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
                InputFailed = false;
                FailureMessage = string.Empty;
                _sequenceEngine.Start(Points, RepeatCount);
            }
        }

        private void ReorderPoints()
        {
            for (int i = 0; i < Points.Count; i++)
            {
                Points[i].Order = i + 1;
            }
            Persist();
        }
    }
}
