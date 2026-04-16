using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PcHostGUI.Infrastructure
{
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private bool _isRunning;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanExecute(object parameter)
        {
            if (IsRunning) return false;
            return _canExecute == null || _canExecute();
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            IsRunning = true;
            try
            {
                await _executeAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}

