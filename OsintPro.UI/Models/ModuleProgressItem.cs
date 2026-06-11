using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace OsintPro.UI.Models
{
    public enum ModuleProgressState
    {
        Pending,
        Running,
        Cached,
        Completed,
        Skipped,
        Cancelled,
        Error
    }

    public class ModuleProgressItem : INotifyPropertyChanged
    {
        private ModuleProgressState _state = ModuleProgressState.Pending;
        private string _statusText = "Очікує";
        private double _progress;
        private bool _isIndeterminate;

        public SearchModule Module { get; init; }
        public string Icon { get; init; } = "";
        public string Title { get; init; } = "";

        public ModuleProgressState State
        {
            get => _state;
            set
            {
                if (_state == value)
                    return;
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBrush));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value)
                    return;
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) < 0.01)
                    return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                if (_isIndeterminate == value)
                    return;
                _isIndeterminate = value;
                OnPropertyChanged();
            }
        }

        public Brush StatusBrush => State switch
        {
            ModuleProgressState.Running => new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
            ModuleProgressState.Cached => new SolidColorBrush(Color.FromRgb(0xB8, 0x6B, 0xFF)),
            ModuleProgressState.Completed => new SolidColorBrush(Color.FromRgb(0x32, 0xCD, 0x32)),
            ModuleProgressState.Skipped => new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            ModuleProgressState.Cancelled => new SolidColorBrush(Color.FromRgb(0xFF, 0x63, 0x47)),
            ModuleProgressState.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x63, 0x47)),
            _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}