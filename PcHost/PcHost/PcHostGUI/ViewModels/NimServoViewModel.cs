using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PcHostGUI.Infrastructure;

namespace PcHostGUI.ViewModels
{
    public sealed class NimServoViewModel : ObservableObject, IDisposable
    {
        private readonly PlcSession _plc;
        private readonly Action<string, string> _log;
        private readonly CancellationToken _appToken;
        private readonly SynchronizationContext _ui;

        private CancellationTokenSource _pollCts;
        private int _pollErrorStreak;

        private bool _enable;
        private bool _powerEnable;
        private int _targetVelocity = 200;
        private bool _commOk;
        private uint _commErrorCount;
        private uint _lastErrorId;
        private byte _cia402State;
        private ushort _statusWord;
        private ushort _controlWord;
        private short _vmActualRpm;
        private short _vmEffRpm;
        private ushort _dcBusVoltage;
        private short _moduleTemp;
        private DateTime _lastTelemetryUtc = DateTime.MinValue;
        private bool _extendedPoll = true;
        private bool _applyVmConfig;

        public NimServoViewModel(PlcSession plc, Action<string, string> log, CancellationToken appToken)
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _appToken = appToken;
            _ui = SynchronizationContext.Current ?? new SynchronizationContext();

            ApplyCommand = new AsyncRelayCommand(ApplyUiToPlcAsync, () => _plc.IsConnected);
            StartVmCommand = new AsyncRelayCommand(StartVmAsync, () => _plc.IsConnected);
            StopCommand = new AsyncRelayCommand(StopAsync, () => _plc.IsConnected);
            ResetCommand = new AsyncRelayCommand(ResetAsync, () => _plc.IsConnected);
        }

        public bool Enable
        {
            get => _enable;
            set => SetProperty(ref _enable, value);
        }

        public bool PowerEnable
        {
            get => _powerEnable;
            set => SetProperty(ref _powerEnable, value);
        }

        public int TargetVelocity
        {
            get => _targetVelocity;
            set => SetProperty(ref _targetVelocity, value);
        }

        public bool ExtendedPoll
        {
            get => _extendedPoll;
            set => SetProperty(ref _extendedPoll, value);
        }

        public bool ApplyVmConfig
        {
            get => _applyVmConfig;
            set => SetProperty(ref _applyVmConfig, value);
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

        public byte Cia402State
        {
            get => _cia402State;
            private set => SetProperty(ref _cia402State, value);
        }

        public ushort StatusWord
        {
            get => _statusWord;
            private set => SetProperty(ref _statusWord, value);
        }

        public ushort ControlWord
        {
            get => _controlWord;
            private set => SetProperty(ref _controlWord, value);
        }

        public short VmActualRpm
        {
            get => _vmActualRpm;
            private set => SetProperty(ref _vmActualRpm, value);
        }

        public short VmTargetEffRpm
        {
            get => _vmEffRpm;
            private set => SetProperty(ref _vmEffRpm, value);
        }

        public ushort DcBusVoltage
        {
            get => _dcBusVoltage;
            private set => SetProperty(ref _dcBusVoltage, value);
        }

        public short ModuleTemp
        {
            get => _moduleTemp;
            private set => SetProperty(ref _moduleTemp, value);
        }

        public DateTime LastTelemetryUtc
        {
            get => _lastTelemetryUtc;
            private set => SetProperty(ref _lastTelemetryUtc, value);
        }

        public AsyncRelayCommand ApplyCommand { get; }
        public AsyncRelayCommand StartVmCommand { get; }
        public AsyncRelayCommand StopCommand { get; }
        public AsyncRelayCommand ResetCommand { get; }

        public void StartPolling()
        {
            if (_pollCts != null) return;

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
                    var snap = await _plc.ExecuteAsync(c =>
                    {
                        return new Snapshot
                        {
                            CommOk = c.ReadSymbol<bool>("GVL_NimServo.CommOk"),
                            CommErrorCount = c.ReadSymbol<uint>("GVL_NimServo.CommErrorCount"),
                            LastErrorId = c.ReadSymbol<uint>("GVL_NimServo.LastErrorId"),
                            Cia402State = c.ReadSymbol<byte>("GVL_NimServo.Cia402State"),
                            StatusWord = c.ReadSymbol<ushort>("GVL_NimServo.StatusWord"),
                            ControlWord = c.ReadSymbol<ushort>("GVL_NimServo.ControlWord"),
                            VmTargetEffRpm = c.ReadSymbol<short>("GVL_NimServo.VmTargetSpeedEffRpm"),
                            VmActualRpm = c.ReadSymbol<short>("GVL_NimServo.VmActualSpeedRpm"),
                            DcBusVoltage = c.ReadSymbol<ushort>("GVL_NimServo.DcBusVoltage"),
                            ModuleTemp = c.ReadSymbol<short>("GVL_NimServo.ModuleTemp"),
                        };
                    }, ct).ConfigureAwait(false);

                    _pollErrorStreak = 0;
                    _ui.Post(_ =>
                    {
                        CommOk = snap.CommOk;
                        CommErrorCount = snap.CommErrorCount;
                        LastErrorId = snap.LastErrorId;
                        Cia402State = snap.Cia402State;
                        StatusWord = snap.StatusWord;
                        ControlWord = snap.ControlWord;
                        VmTargetEffRpm = snap.VmTargetEffRpm;
                        VmActualRpm = snap.VmActualRpm;
                        DcBusVoltage = snap.DcBusVoltage;
                        ModuleTemp = snap.ModuleTemp;
                        LastTelemetryUtc = DateTime.UtcNow;
                    }, null);
                }
                catch (Exception ex)
                {
                    _pollErrorStreak++;
                    if (_pollErrorStreak == 1 || _pollErrorStreak % 50 == 0)
                    {
                        _log("WARN", "NimServo poll error (" + _pollErrorStreak.ToString(CultureInfo.InvariantCulture) + "): " + ex.GetType().Name);
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
                await _plc.WriteAsync("GVL_NimServo.ExtendedPoll", ExtendedPoll, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.ApplyVmConfig", ApplyVmConfig, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.Enable", Enable, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.PowerEnable", PowerEnable, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.TargetVelocity", TargetVelocity, ct).ConfigureAwait(true);
                _log("INFO", "Applied NimServo settings.");
            }
            catch (Exception ex)
            {
                _log("ERROR", "Apply failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task ResetAsync()
        {
            var ct = _appToken;
            try
            {
                await _plc.WriteAsync("GVL_NimServo.PowerEnable", false, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.Enable", false, ct).ConfigureAwait(true);
                Enable = false;
                PowerEnable = false;
                _log("INFO", "NimServo reset: PowerEnable=false, Enable=false");
            }
            catch (Exception ex)
            {
                _log("ERROR", "Reset failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task StopAsync()
        {
            var ct = _appToken;
            try
            {
                await _plc.WriteAsync("GVL_NimServo.TargetVelocity", 0, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.PowerEnable", false, ct).ConfigureAwait(true);
                PowerEnable = false;
                _log("INFO", "NimServo stop issued.");
            }
            catch (Exception ex)
            {
                _log("ERROR", "Stop failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task StartVmAsync()
        {
            var ct = _appToken;
            try
            {
                await _plc.WriteAsync("GVL_NimServo.DesiredMode", (byte)2, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.ExtendedPoll", ExtendedPoll, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.ApplyVmConfig", ApplyVmConfig, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.Enable", true, ct).ConfigureAwait(true);
                await _plc.WriteAsync("GVL_NimServo.PowerEnable", true, ct).ConfigureAwait(true);

                Enable = true;
                PowerEnable = true;

                // Wait for OP enabled
                DateTime deadline = DateTime.UtcNow.AddSeconds(6);
                while (DateTime.UtcNow < deadline)
                {
                    byte cia = await _plc.ReadAsync<byte>("GVL_NimServo.Cia402State", ct).ConfigureAwait(true);
                    if (cia == 5) break;
                    await Task.Delay(50, ct).ConfigureAwait(true);
                }

                await _plc.WriteAsync("GVL_NimServo.TargetVelocity", TargetVelocity, ct).ConfigureAwait(true);
                _log("INFO", "Start VM issued. TargetVelocity=" + TargetVelocity.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _log("ERROR", "Start VM failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Dispose()
        {
            StopPolling();
        }

        private struct Snapshot
        {
            public bool CommOk;
            public uint CommErrorCount;
            public uint LastErrorId;
            public byte Cia402State;
            public ushort StatusWord;
            public ushort ControlWord;
            public short VmActualRpm;
            public short VmTargetEffRpm;
            public ushort DcBusVoltage;
            public short ModuleTemp;
        }
    }
}
