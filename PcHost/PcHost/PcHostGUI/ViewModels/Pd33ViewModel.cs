using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PcHostGUI.Infrastructure;

namespace PcHostGUI.ViewModels
{
    public sealed class Pd33ViewModel : ObservableObject, IDisposable
    {
        private readonly PlcSession _plc;
        private readonly Action<string, string> _log;
        private readonly CancellationToken _appToken;
        private readonly SynchronizationContext _ui;

        private CancellationTokenSource _pollCts;
        private int _pollErrorStreak;

        private bool _enable;
        private int _channel = 1;
        private int _slaveId = 1;
        private bool _wordSwap32 = true;
        private bool _useDisplayRegister3B;

        private bool _channelConflict;
        private bool _busy;
        private bool _commOk;
        private uint _commErrorCount;
        private uint _lastErrorId;
        private bool _portReady;
        private uint _rxDiscardCount;
        private uint _lastDiscardId;
        private byte _exceptionCode;

        private int _absRaw;
        private int _relRaw;
        private double _absMm;
        private double _relMm;
        private ushort _zeroWord;
        private DateTime _lastTelemetryUtc = DateTime.MinValue;

        public Pd33ViewModel(PlcSession plc, Action<string, string> log, CancellationToken appToken)
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _appToken = appToken;
            _ui = SynchronizationContext.Current ?? new SynchronizationContext();

            ApplyCommand = new AsyncRelayCommand(ApplyUiToPlcAsync, () => _plc.IsConnected);
            ZeroCommand = new AsyncRelayCommand(() => IssueCommandAsync(1), () => _plc.IsConnected);
            CancelZeroCommand = new AsyncRelayCommand(() => IssueCommandAsync(2), () => _plc.IsConnected);
        }

        public bool Enable
        {
            get => _enable;
            set => SetProperty(ref _enable, value);
        }

        public byte Channel
        {
            get => (byte)_channel;
            set => SetProperty(ref _channel, value);
        }

        public int ChannelInt
        {
            get => _channel;
            set => SetProperty(ref _channel, value);
        }

        public byte SlaveId
        {
            get => (byte)_slaveId;
            set => SetProperty(ref _slaveId, value);
        }

        public int SlaveIdInt
        {
            get => _slaveId;
            set => SetProperty(ref _slaveId, value);
        }

        public bool WordSwap32
        {
            get => _wordSwap32;
            set => SetProperty(ref _wordSwap32, value);
        }

        public bool UseDisplayRegister3B
        {
            get => _useDisplayRegister3B;
            set => SetProperty(ref _useDisplayRegister3B, value);
        }

        public bool ChannelConflict
        {
            get => _channelConflict;
            private set => SetProperty(ref _channelConflict, value);
        }

        public bool Busy
        {
            get => _busy;
            private set => SetProperty(ref _busy, value);
        }

        public bool CommOk
        {
            get => _commOk;
            private set => SetProperty(ref _commOk, value);
        }

        public uint CommErrorCount
        {
            get => _commErrorCount;
            private set => SetProperty(ref _commErrorCount, value);
        }

        public uint LastErrorId
        {
            get => _lastErrorId;
            private set => SetProperty(ref _lastErrorId, value);
        }

        public bool PortReady
        {
            get => _portReady;
            private set => SetProperty(ref _portReady, value);
        }

        public uint RxDiscardCount
        {
            get => _rxDiscardCount;
            private set => SetProperty(ref _rxDiscardCount, value);
        }

        public uint LastDiscardId
        {
            get => _lastDiscardId;
            private set => SetProperty(ref _lastDiscardId, value);
        }

        public byte ExceptionCode
        {
            get => _exceptionCode;
            private set => SetProperty(ref _exceptionCode, value);
        }

        public int AbsRaw
        {
            get => _absRaw;
            private set => SetProperty(ref _absRaw, value);
        }

        public int RelRaw
        {
            get => _relRaw;
            private set => SetProperty(ref _relRaw, value);
        }

        public double AbsMm
        {
            get => _absMm;
            private set => SetProperty(ref _absMm, value);
        }

        public double RelMm
        {
            get => _relMm;
            private set => SetProperty(ref _relMm, value);
        }

        public ushort ZeroWord
        {
            get => _zeroWord;
            private set => SetProperty(ref _zeroWord, value);
        }

        public DateTime LastTelemetryUtc
        {
            get => _lastTelemetryUtc;
            private set => SetProperty(ref _lastTelemetryUtc, value);
        }

        public AsyncRelayCommand ApplyCommand { get; }
        public AsyncRelayCommand ZeroCommand { get; }
        public AsyncRelayCommand CancelZeroCommand { get; }

        public void StartPolling()
        {
            if (_pollCts != null) return;
            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(_appToken);
            _ = Task.Run(() => PollLoopAsync(_pollCts.Token), _pollCts.Token);
            _ = Task.Run(() => LoadConfigOnceAsync(_pollCts.Token), _pollCts.Token);
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
                    var snap = await _plc.ExecuteAsync(c =>
                    {
                        return new Snapshot
                        {
                            ChannelConflict = c.ReadSymbol<bool>("GVL_PD33.ChannelConflict"),
                            Busy = c.ReadSymbol<bool>("GVL_PD33.Busy"),
                            PortReady = c.ReadSymbol<bool>("GVL_PD33.PortReady"),
                            CommOk = c.ReadSymbol<bool>("GVL_PD33.CommOk"),
                            CommErrorCount = c.ReadSymbol<uint>("GVL_PD33.CommErrorCount"),
                            LastErrorId = c.ReadSymbol<uint>("GVL_PD33.LastErrorId"),
                            ExceptionCode = c.ReadSymbol<byte>("GVL_PD33.ExceptionCode"),
                            RxDiscardCount = c.ReadSymbol<uint>("GVL_PD33.RxDiscardCount"),
                            LastDiscardId = c.ReadSymbol<uint>("GVL_PD33.LastDiscardId"),

                            AbsRaw = c.ReadSymbol<int>("GVL_PD33.AbsRaw"),
                            RelRaw = c.ReadSymbol<int>("GVL_PD33.RelRaw"),
                            AbsMm = c.ReadSymbol<double>("GVL_PD33.AbsMm"),
                            RelMm = c.ReadSymbol<double>("GVL_PD33.RelMm"),
                            ZeroWord = c.ReadSymbol<ushort>("GVL_PD33.ZeroWord"),
                        };
                    }, ct).ConfigureAwait(false);

                    _pollErrorStreak = 0;
                    _ui.Post(_ =>
                    {
                        ChannelConflict = snap.ChannelConflict;
                        Busy = snap.Busy;
                        PortReady = snap.PortReady;
                        CommOk = snap.CommOk;
                        CommErrorCount = snap.CommErrorCount;
                        LastErrorId = snap.LastErrorId;
                        ExceptionCode = snap.ExceptionCode;
                        RxDiscardCount = snap.RxDiscardCount;
                        LastDiscardId = snap.LastDiscardId;

                        AbsRaw = snap.AbsRaw;
                        RelRaw = snap.RelRaw;
                        AbsMm = snap.AbsMm;
                        RelMm = snap.RelMm;
                        ZeroWord = snap.ZeroWord;
                        LastTelemetryUtc = DateTime.UtcNow;
                    }, null);
                }
                catch (Exception ex)
                {
                    _pollErrorStreak++;
                    if (_pollErrorStreak == 1 || _pollErrorStreak % 50 == 0)
                    {
                        _log("WARN", "PD33 poll error (" + _pollErrorStreak.ToString(CultureInfo.InvariantCulture) + "): " + ex.GetType().Name);
                    }
                }

                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }

        private async Task ApplyUiToPlcAsync()
        {
            var ct = _appToken;
            try
            {
                byte ch = (byte)Clamp(_channel, 1, 2);
                byte id = (byte)Clamp(_slaveId, 1, 247);
                await _plc.WriteAsync("GVL_PD33.Channel", ch, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_PD33.SlaveId", id, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_PD33.WordSwap32", WordSwap32, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_PD33.UseDisplayRegister3B", UseDisplayRegister3B, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_PD33.Enable", Enable, ct).ConfigureAwait(false);
                _log("INFO", "Applied PD33 settings.");
            }
            catch (Exception ex)
            {
                _log("ERROR", "Apply PD33 failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task LoadConfigOnceAsync(CancellationToken ct)
        {
            try
            {
                var cfg = await _plc.ExecuteAsync(c =>
                {
                    return new ConfigSnapshot
                    {
                        Enable = c.ReadSymbol<bool>("GVL_PD33.Enable"),
                        Channel = c.ReadSymbol<byte>("GVL_PD33.Channel"),
                        SlaveId = c.ReadSymbol<byte>("GVL_PD33.SlaveId"),
                        WordSwap32 = c.ReadSymbol<bool>("GVL_PD33.WordSwap32"),
                        UseDisplayRegister3B = c.ReadSymbol<bool>("GVL_PD33.UseDisplayRegister3B"),
                    };
                }, ct).ConfigureAwait(false);

                _ui.Post(_ =>
                {
                    Enable = cfg.Enable;
                    _channel = cfg.Channel;
                    OnPropertyChanged(nameof(ChannelInt));
                    _slaveId = cfg.SlaveId;
                    OnPropertyChanged(nameof(SlaveIdInt));
                    WordSwap32 = cfg.WordSwap32;
                    UseDisplayRegister3B = cfg.UseDisplayRegister3B;
                }, null);
            }
            catch
            {
                // ignore
            }
        }

        private async Task IssueCommandAsync(byte cmd)
        {
            var ct = _appToken;
            try
            {
                uint seq = await _plc.ReadAsync<uint>("GVL_PD33.CmdSeq", ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_PD33.Cmd", cmd, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_PD33.CmdSeq", seq + 1, ct).ConfigureAwait(false);

                if (cmd == 1)
                {
                    _log("INFO", "PD33 zero requested.");
                }
                else if (cmd == 2)
                {
                    _log("INFO", "PD33 cancel zero requested.");
                }
                else
                {
                    _log("INFO", "PD33 cmd=" + cmd.ToString(CultureInfo.InvariantCulture) + " issued.");
                }
            }
            catch (Exception ex)
            {
                _log("ERROR", "PD33 cmd failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Dispose()
        {
            StopPolling();
        }

        private struct Snapshot
        {
            public bool ChannelConflict;
            public bool Busy;
            public bool PortReady;
            public bool CommOk;
            public uint CommErrorCount;
            public uint LastErrorId;
            public byte ExceptionCode;
            public uint RxDiscardCount;
            public uint LastDiscardId;

            public int AbsRaw;
            public int RelRaw;
            public double AbsMm;
            public double RelMm;
            public ushort ZeroWord;
        }

        private struct ConfigSnapshot
        {
            public bool Enable;
            public byte Channel;
            public byte SlaveId;
            public bool WordSwap32;
            public bool UseDisplayRegister3B;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
