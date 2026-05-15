using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PcHost.Core;
using PcHostGUI.Infrastructure;
namespace PcHostGUI.ViewModels
{
    public sealed class MainViewModel : ObservableObject, IDisposable
    {
        private readonly CancellationTokenSource _appCts = new CancellationTokenSource();
        private readonly PlcSession _plc = new PlcSession();

        private string _amsNetId = "5.132.114.206.1.1";
        private int _port = PlcSymbols.DefaultPlcPort;
        private bool _isConnected;
        private string _statusText = "Disconnected";
        private string _latestLogMessage = "Ready";

        public MainViewModel()
        {
            Logs = new ObservableCollection<UiLog>();

            ControlPanel = new ControlPanelViewModel(_plc, AddLog, _appCts.Token);

            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsConnected);
            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => IsConnected);
            ClearLogsCommand = new RelayCommand(ClearLogs);
        }

        public string AmsNetId
        {
            get => _amsNetId;
            set => SetProperty(ref _amsNetId, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionBadge));
                }
            }
        }

        public string ConnectionBadge => IsConnected ? "CONNECTED" : "DISCONNECTED";

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string LatestLogMessage
        {
            get => _latestLogMessage;
            private set => SetProperty(ref _latestLogMessage, value);
        }

        public ObservableCollection<UiLog> Logs { get; }

        public ControlPanelViewModel ControlPanel { get; }

        public AsyncRelayCommand ConnectCommand { get; }
        public AsyncRelayCommand DisconnectCommand { get; }
        public RelayCommand ClearLogsCommand { get; }

        private async Task ConnectAsync()
        {
            try
            {
                StatusText = "Connecting...";
                AddLog("INFO", "Connecting to " + AmsNetId + ":" + Port.ToString(CultureInfo.InvariantCulture));

                var settings = new PlcConnectionSettings(AmsNetId, Port);
                await Task.Run(() => _plc.Connect(settings), _appCts.Token).ConfigureAwait(true);
                IsConnected = true;
                StatusText = "Connected";
                AddLog("INFO", "Connected. No device logic is started automatically.");
            }
            catch (Exception ex)
            {
                AddLog("ERROR", ex.GetType().Name + ": " + ex.Message);
                StatusText = "Connect failed";
                IsConnected = false;
                _plc.Disconnect();
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                await ControlPanel.StopPlcActionsAsync(_appCts.Token).ConfigureAwait(true);
            }
            catch
            {
                ControlPanel.StopLocalLoops();
            }

            _plc.Disconnect();
            IsConnected = false;
            StatusText = "Disconnected";
            AddLog("INFO", "Disconnected");
        }

        private void AddLog(string level, string message)
        {
            if (message == null) message = string.Empty;
            Logs.Insert(0, new UiLog(DateTime.UtcNow, level, message));
            LatestLogMessage = message;
            while (Logs.Count > 500)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        }

        private void ClearLogs()
        {
            Logs.Clear();
            LatestLogMessage = "Logs cleared";
        }

        public void Dispose()
        {
            try { _appCts.Cancel(); } catch { }
            ControlPanel.Dispose();
            _plc.Dispose();
            _appCts.Dispose();
        }
    }
}
