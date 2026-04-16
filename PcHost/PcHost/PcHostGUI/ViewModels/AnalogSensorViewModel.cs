using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PcHostGUI.Infrastructure;

namespace PcHostGUI.ViewModels
{
    public sealed class AnalogSensorViewModel : ObservableObject, IDisposable
    {
        private readonly string _name;
        private readonly Action<string, string> _log;
        private readonly PlcSession _plc;
        private readonly CancellationToken _appToken;
        private readonly SynchronizationContext _ui;

        private CancellationTokenSource _pollCts;

        private string _symbol;
        private short _raw;
        private string _status = "Idle";
        private int _pollMs = 50;
        private int _pollErrorStreak;

        public AnalogSensorViewModel(string name, string defaultSymbol, Action<string, string> log, PlcSession plc, CancellationToken appToken)
        {
            _name = name ?? "Analog";
            _symbol = defaultSymbol ?? string.Empty;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _appToken = appToken;
            _ui = SynchronizationContext.Current ?? new SynchronizationContext();

            ReadOnceCommand = new AsyncRelayCommand(ReadOnceAsync, () => _plc.IsConnected);
        }

        public string Name => _name;

        public string Symbol
        {
            get => _symbol;
            set => SetProperty(ref _symbol, value);
        }

        public short Raw
        {
            get => _raw;
            private set => SetProperty(ref _raw, value);
        }

        public int PollMs
        {
            get => _pollMs;
            set => SetProperty(ref _pollMs, value);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public AsyncRelayCommand ReadOnceCommand { get; }

        public void StartPolling()
        {
            if (_pollCts != null) return;
            if (string.IsNullOrWhiteSpace(Symbol)) return;

            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(_appToken);
            _ = Task.Run(() => PollLoopAsync(_pollCts.Token), _pollCts.Token);
        }

        public void StopPolling()
        {
            if (_pollCts == null) return;
            try { _pollCts.Cancel(); } catch { }
            try { _pollCts.Dispose(); } catch { }
            _pollCts = null;
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(Symbol))
                    {
                        short raw = await _plc.ExecuteAsync(c => c.ReadSymbol<short>(Symbol), ct).ConfigureAwait(false);
                        _pollErrorStreak = 0;
                        _ui.Post(_ =>
                        {
                            Raw = raw;
                            Status = "OK";
                        }, null);
                    }
                }
                catch (Exception ex)
                {
                    _pollErrorStreak++;
                    string status = ex.GetType().Name;
                    _ui.Post(_ => Status = status, null);
                    if (_pollErrorStreak == 1 || _pollErrorStreak % 200 == 0)
                    {
                        _log("WARN", Name + " poll error (" + _pollErrorStreak.ToString(CultureInfo.InvariantCulture) + "): " + status);
                    }
                }

                int delay = PollMs < 10 ? 10 : PollMs;
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        private async Task ReadOnceAsync()
        {
            var ct = _appToken;
            if (string.IsNullOrWhiteSpace(Symbol))
            {
                _log("WARN", Name + ": Symbol is empty.");
                return;
            }

            try
            {
                short raw = await _plc.ExecuteAsync(c => c.ReadSymbol<short>(Symbol), ct).ConfigureAwait(false);
                _ui.Post(_ =>
                {
                    Raw = raw;
                    Status = "OK";
                }, null);
                _log("INFO", Name + " raw=" + raw.ToString(CultureInfo.InvariantCulture) + " (" + Symbol + ")");
            }
            catch (Exception ex)
            {
                string status = ex.GetType().Name;
                _ui.Post(_ => Status = status, null);
                _log("ERROR", Name + " read failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Dispose()
        {
            StopPolling();
        }
    }
}
