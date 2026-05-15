using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using PcHost.Core;
using PcHostGUI.Infrastructure;

namespace PcHostGUI.ViewModels
{
    public sealed class ControlPanelViewModel : ObservableObject, IDisposable
    {
        private readonly PlcSession _plc;
        private readonly Action<string, string> _log;
        private readonly CancellationToken _appToken;
        private readonly SynchronizationContext _ui;

        private CancellationTokenSource _logCts;
        private bool _pd33StartedByPanel;
        private bool _loggerStartedByPanel;
        private string _status = "Idle";

        private bool _usePd33;
        private bool _useVibration;
        private bool _usePressure;
        private bool _useTorque;
        private bool _useValves;

        private bool _valveEnable;
        private bool _valve1;
        private bool _valve2;
        private bool _valve3;
        private bool _valve4;
        private bool _brakeValve1 = true;
        private bool _brakeValve2 = true;
        private bool _brakeValve3;
        private bool _brakeValve4;

        private bool _loggerEnable;
        private uint _loggerHead;
        private uint _loggerTail;
        private uint _loggerOverrun;
        private string _logFilePath = "vbm_log.csv";
        private int _pollMs = 20;

        public ControlPanelViewModel(PlcSession plc, Action<string, string> log, CancellationToken appToken)
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _appToken = appToken;
            _ui = SynchronizationContext.Current ?? new SynchronizationContext();

            ApplyValveCommand = new AsyncRelayCommand(ApplyValveAsync, CanControlValves);
            RefreshValveCommand = new AsyncRelayCommand(RefreshValveAsync, () => _plc.IsConnected);
            BrakeCommand = new AsyncRelayCommand(BrakeAsync, CanControlValves);
            ReleaseCommand = new AsyncRelayCommand(ReleaseAsync, CanControlValves);
            LockCommand = new AsyncRelayCommand(LockAsync, CanControlValves);

            ToggleRecordingCommand = new AsyncRelayCommand(ToggleRecordingAsync, () => _plc.IsConnected);
            ResetLoggerCommand = new AsyncRelayCommand(ResetLoggerAsync, () => _plc.IsConnected && !LoggerEnable);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public bool UsePd33
        {
            get => _usePd33;
            set => SetProperty(ref _usePd33, value);
        }

        public bool UseVibration
        {
            get => _useVibration;
            set => SetProperty(ref _useVibration, value);
        }

        public bool UsePressure
        {
            get => _usePressure;
            set => SetProperty(ref _usePressure, value);
        }

        public bool UseTorque
        {
            get => _useTorque;
            set => SetProperty(ref _useTorque, value);
        }

        public bool UseValves
        {
            get => _useValves;
            set => SetProperty(ref _useValves, value);
        }

        public bool ValveEnable
        {
            get => _valveEnable;
            private set => SetProperty(ref _valveEnable, value);
        }

        public bool Valve1 { get => _valve1; set => SetProperty(ref _valve1, value); }
        public bool Valve2 { get => _valve2; set => SetProperty(ref _valve2, value); }
        public bool Valve3 { get => _valve3; set => SetProperty(ref _valve3, value); }
        public bool Valve4 { get => _valve4; set => SetProperty(ref _valve4, value); }

        public bool BrakeValve1 { get => _brakeValve1; set => SetProperty(ref _brakeValve1, value); }
        public bool BrakeValve2 { get => _brakeValve2; set => SetProperty(ref _brakeValve2, value); }
        public bool BrakeValve3 { get => _brakeValve3; set => SetProperty(ref _brakeValve3, value); }
        public bool BrakeValve4 { get => _brakeValve4; set => SetProperty(ref _brakeValve4, value); }

        public bool LoggerEnable
        {
            get => _loggerEnable;
            private set
            {
                if (SetProperty(ref _loggerEnable, value))
                {
                    OnPropertyChanged(nameof(RecordingButtonText));
                }
            }
        }

        public string RecordingButtonText => LoggerEnable ? "停止记录" : "开始记录";
        public uint LoggerHead { get => _loggerHead; private set => SetProperty(ref _loggerHead, value); }
        public uint LoggerTail { get => _loggerTail; private set => SetProperty(ref _loggerTail, value); }
        public uint LoggerOverrun { get => _loggerOverrun; private set => SetProperty(ref _loggerOverrun, value); }

        public string LogFilePath
        {
            get => _logFilePath;
            set => SetProperty(ref _logFilePath, value);
        }

        public int PollMs
        {
            get => _pollMs;
            set => SetProperty(ref _pollMs, value);
        }

        public AsyncRelayCommand ApplyValveCommand { get; }
        public AsyncRelayCommand RefreshValveCommand { get; }
        public AsyncRelayCommand BrakeCommand { get; }
        public AsyncRelayCommand ReleaseCommand { get; }
        public AsyncRelayCommand LockCommand { get; }
        public AsyncRelayCommand ToggleRecordingCommand { get; }
        public AsyncRelayCommand ResetLoggerCommand { get; }

        private bool CanControlValves()
        {
            return _plc.IsConnected && UseValves;
        }

        private async Task ApplyValveAsync()
        {
            await WriteValvesAsync(Valve1, Valve2, Valve3, Valve4, true, "Manual valve pattern applied.").ConfigureAwait(false);
        }

        private async Task BrakeAsync()
        {
            await WriteValvesAsync(BrakeValve1, BrakeValve2, BrakeValve3, BrakeValve4, true, "Brake valve pattern applied.").ConfigureAwait(false);
        }

        private async Task ReleaseAsync()
        {
            await WriteValvesAsync(!BrakeValve1, !BrakeValve2, !BrakeValve3, !BrakeValve4, true, "Release valve pattern applied.").ConfigureAwait(false);
        }

        private async Task LockAsync()
        {
            await WriteValvesAsync(false, false, false, false, false, "Locked: all valve outputs OFF.").ConfigureAwait(false);
        }

        private async Task WriteValvesAsync(bool valve1, bool valve2, bool valve3, bool valve4, bool enable, string message)
        {
            var ct = _appToken;
            try
            {
                if (enable)
                {
                    await _plc.WriteAsync("GVL_ValveIO.Enable", true, ct).ConfigureAwait(false);
                }
                await _plc.WriteAsync("GVL_ValveIO.Valve1_Cmd", valve1, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve2_Cmd", valve2, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve3_Cmd", valve3, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve4_Cmd", valve4, ct).ConfigureAwait(false);
                if (!enable)
                {
                    await _plc.WriteAsync("GVL_ValveIO.Enable", false, ct).ConfigureAwait(false);
                }
                _log("INFO", message);
                await RefreshValveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log("ERROR", "Valve write failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task RefreshValveAsync()
        {
            var ct = _appToken;
            try
            {
                bool en = await _plc.ReadAsync<bool>("GVL_ValveIO.Enable", ct).ConfigureAwait(false);
                bool v1 = await _plc.ReadAsync<bool>("GVL_ValveIO.Valve1_Fb", ct).ConfigureAwait(false);
                bool v2 = await _plc.ReadAsync<bool>("GVL_ValveIO.Valve2_Fb", ct).ConfigureAwait(false);
                bool v3 = await _plc.ReadAsync<bool>("GVL_ValveIO.Valve3_Fb", ct).ConfigureAwait(false);
                bool v4 = await _plc.ReadAsync<bool>("GVL_ValveIO.Valve4_Fb", ct).ConfigureAwait(false);

                _ui.Post(_ =>
                {
                    ValveEnable = en;
                    Valve1 = v1;
                    Valve2 = v2;
                    Valve3 = v3;
                    Valve4 = v4;
                }, null);
            }
            catch (Exception ex)
            {
                _log("WARN", "Valve refresh failed: " + ex.GetType().Name);
            }
        }

        private async Task ResetLoggerAsync()
        {
            var ct = _appToken;
            try
            {
                await _plc.WriteAsync("GVL_DataLogger.Reset", true, ct).ConfigureAwait(false);
                await Task.Delay(60, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_DataLogger.Reset", false, ct).ConfigureAwait(false);
                LoggerHead = 0;
                LoggerTail = 0;
                LoggerOverrun = 0;
                _log("INFO", "Logger reset requested.");
            }
            catch (Exception ex)
            {
                _log("ERROR", "Reset logger failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task ToggleRecordingAsync()
        {
            if (LoggerEnable)
            {
                await StopRecordingAsync().ConfigureAwait(false);
            }
            else
            {
                await StartRecordingAsync().ConfigureAwait(false);
            }
        }

        private async Task StartRecordingAsync()
        {
            var ct = _appToken;
            if (_logCts != null) return;

            if (!UsePd33 && !UseVibration && !UsePressure && !UseTorque && !UseValves)
            {
                _log("WARN", "No peripherals selected; recording not started.");
                Status = "No peripherals selected";
                return;
            }

            try
            {
                Status = "Starting recording...";

                _pd33StartedByPanel = false;
                _loggerStartedByPanel = false;
                if (UsePd33)
                {
                    await _plc.WriteAsync("GVL_PD33.Channel", (byte)1, ct).ConfigureAwait(false);
                    await _plc.WriteAsync("GVL_PD33.UseDisplayRegister3B", false, ct).ConfigureAwait(false);
                    await _plc.WriteAsync("GVL_PD33.Enable", true, ct).ConfigureAwait(false);
                    _pd33StartedByPanel = true;
                }

                await _plc.WriteAsync("GVL_Vibration.Channel", (byte)2, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_Vibration.SlaveId", (byte)0x50, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_Vibration.MinGapMs", (ushort)200, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_Vibration.TimeoutMs", (ushort)500, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_Vibration.Enable", UseVibration, ct).ConfigureAwait(false);

                await _plc.WriteAsync("GVL_DataLogger.Enable", true, ct).ConfigureAwait(false);
                _loggerStartedByPanel = true;
                LoggerEnable = true;

                _logCts = CancellationTokenSource.CreateLinkedTokenSource(_appToken);
                _ = Task.Run(() => LogLoopAsync(_logCts.Token), _logCts.Token);

                Status = "Recording";
                _log("INFO", "Recording started: " + BuildSelectionText());
            }
            catch (Exception ex)
            {
                Status = "Start failed";
                _log("ERROR", "Start recording failed: " + ex.GetType().Name + ": " + ex.Message);
                if (_pd33StartedByPanel)
                {
                    try { await _plc.WriteAsync("GVL_PD33.Enable", false, ct).ConfigureAwait(false); } catch { }
                }
                if (_loggerStartedByPanel)
                {
                    try { await _plc.WriteAsync("GVL_DataLogger.Enable", false, ct).ConfigureAwait(false); } catch { }
                }
                try { _logCts?.Cancel(); } catch { }
                try { _logCts?.Dispose(); } catch { }
                _logCts = null;
                _pd33StartedByPanel = false;
                _loggerStartedByPanel = false;
                LoggerEnable = false;
            }
        }

        private async Task StopRecordingAsync()
        {
            var ct = _appToken;
            if (_logCts == null) return;

            Status = "Stopping recording...";
            try { _logCts.Cancel(); } catch { }
            try { _logCts.Dispose(); } catch { }
            _logCts = null;

            if (_loggerStartedByPanel)
            {
                try { await _plc.WriteAsync("GVL_DataLogger.Enable", false, ct).ConfigureAwait(false); } catch { }
            }
            if (_pd33StartedByPanel)
            {
                try { await _plc.WriteAsync("GVL_PD33.Enable", false, ct).ConfigureAwait(false); } catch { }
            }
            try { await _plc.WriteAsync("GVL_Vibration.Enable", false, ct).ConfigureAwait(false); } catch { }
            _pd33StartedByPanel = false;
            _loggerStartedByPanel = false;

            LoggerEnable = false;
            Status = "Stopped";
            _log("INFO", "Recording stopped.");
        }

        public void StopLocalLoops()
        {
            try { _logCts?.Cancel(); } catch { }
            try { _logCts?.Dispose(); } catch { }
            _logCts = null;
            LoggerEnable = false;
            Status = "Stopped";
        }

        public async Task StopPlcActionsAsync(CancellationToken ct)
        {
            StopLocalLoops();

            if (!_plc.IsConnected)
            {
                _pd33StartedByPanel = false;
                return;
            }

            if (_loggerStartedByPanel)
            {
                try { await _plc.WriteAsync("GVL_DataLogger.Enable", false, ct).ConfigureAwait(false); } catch { }
            }
            if (_pd33StartedByPanel)
            {
                try { await _plc.WriteAsync("GVL_PD33.Enable", false, ct).ConfigureAwait(false); } catch { }
            }
            try { await _plc.WriteAsync("GVL_Vibration.Enable", false, ct).ConfigureAwait(false); } catch { }
            _pd33StartedByPanel = false;
            _loggerStartedByPanel = false;
        }

        private async Task LogLoopAsync(CancellationToken ct)
        {
            const string headSymbol = "GVL_DataLogger.Head";
            const string tailSymbol = "GVL_DataLogger.Tail";
            const string bufferSymbol = "GVL_DataLogger.Buffer";
            const int bufferSize = VariableBladeLogRecord.SizeBytes * 4096;

            byte[] carry = Array.Empty<byte>();
            var cursor = new RingBufferCursor(0);

            bool includePd33 = UsePd33;
            bool includeVibration = UseVibration;
            bool includePressure = UsePressure;
            bool includeTorque = UseTorque;
            bool includeValves = UseValves;

            string path = LogFilePath;
            if (string.IsNullOrWhiteSpace(path)) path = "vbm_log.csv";
            path = Path.GetFullPath(path);

            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                writer.WriteLine("pc_utc_iso,seq,t_us,pressure_raw,torque_raw,pd33_rel_raw,pd33_rel_mm,vib_acc_x,vib_acc_y,vib_acc_z,vib_vel_x,vib_vel_y,vib_vel_z,vib_temp_c,vib_disp_x,vib_disp_y,vib_disp_z,vib_freq_x,vib_freq_y,vib_freq_z,valves_bits,flags,selected_devices");
                writer.Flush();

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        byte[] data = await _plc.ExecuteAsync(c => RingBufferReader.ReadNewBytes(c, headSymbol, bufferSymbol, bufferSize, cursor), ct).ConfigureAwait(false);
                        if (data.Length > 0)
                        {
                            try { await _plc.WriteAsync(tailSymbol, cursor.NextIndex, ct).ConfigureAwait(false); } catch { }

                            int totalLen = carry.Length + data.Length;
                            byte[] merged = new byte[totalLen];
                            Buffer.BlockCopy(carry, 0, merged, 0, carry.Length);
                            Buffer.BlockCopy(data, 0, merged, carry.Length, data.Length);

                            int offset = 0;
                            while (offset + VariableBladeLogRecord.SizeBytes <= merged.Length)
                            {
                                if (VariableBladeLogRecord.TryParse(new ReadOnlySpan<byte>(merged, offset, VariableBladeLogRecord.SizeBytes), out var rec))
                                {
                                    string ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                                    writer.WriteLine(
                                        ts + "," +
                                        rec.Seq.ToString(CultureInfo.InvariantCulture) + "," +
                                        rec.TimeUs.ToString(CultureInfo.InvariantCulture) + "," +
                                        Field(includePressure, rec.PressureRaw.ToString(CultureInfo.InvariantCulture)) + "," +
                                        Field(includeTorque, rec.TorqueRaw.ToString(CultureInfo.InvariantCulture)) + "," +
                                        Field(includePd33, rec.Pd33RelRaw.ToString(CultureInfo.InvariantCulture)) + "," +
                                        Field(includePd33, (rec.Pd33RelRaw / 1000.0).ToString("F6", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibAccX.ToString("F6", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibAccY.ToString("F6", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibAccZ.ToString("F6", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibVelX.ToString("F2", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibVelY.ToString("F2", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibVelZ.ToString("F2", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibTempC.ToString("F2", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibDispX.ToString("F0", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibDispY.ToString("F0", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibDispZ.ToString("F0", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibFreqX.ToString("F1", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibFreqY.ToString("F1", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeVibration, rec.VibFreqZ.ToString("F1", CultureInfo.InvariantCulture)) + "," +
                                        Field(includeValves, rec.ValvesBits.ToString(CultureInfo.InvariantCulture)) + "," +
                                        rec.Flags.ToString(CultureInfo.InvariantCulture) + "," +
                                        BuildSelectionText(includePd33, includeVibration, includePressure, includeTorque, includeValves)
                                    );
                                    offset += VariableBladeLogRecord.SizeBytes;
                                }
                                else
                                {
                                    offset += 1;
                                }
                            }

                            int remaining = merged.Length - offset;
                            if (remaining > 0)
                            {
                                carry = new byte[remaining];
                                Buffer.BlockCopy(merged, offset, carry, 0, remaining);
                            }
                            else
                            {
                                carry = Array.Empty<byte>();
                            }

                            writer.Flush();
                        }

                        uint head = await _plc.ReadAsync<uint>("GVL_DataLogger.Head", ct).ConfigureAwait(false);
                        uint tail = await _plc.ReadAsync<uint>("GVL_DataLogger.Tail", ct).ConfigureAwait(false);
                        uint ov = await _plc.ReadAsync<uint>("GVL_DataLogger.OverrunCount", ct).ConfigureAwait(false);
                        _ui.Post(_ =>
                        {
                            LoggerHead = head;
                            LoggerTail = tail;
                            LoggerOverrun = ov;
                        }, null);
                    }
                    catch (Exception ex)
                    {
                        _ui.Post(_ => Status = ex.GetType().Name, null);
                    }

                    int delay = PollMs < 5 ? 5 : PollMs;
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        private string BuildSelectionText()
        {
            return BuildSelectionText(UsePd33, UseVibration, UsePressure, UseTorque, UseValves);
        }

        private static string BuildSelectionText(bool pd33, bool vibration, bool pressure, bool torque, bool valves)
        {
            var sb = new StringBuilder();
            AppendSelected(sb, pd33, "pd33");
            AppendSelected(sb, vibration, "vibration");
            AppendSelected(sb, pressure, "pressure");
            AppendSelected(sb, torque, "torque");
            AppendSelected(sb, valves, "valves");
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        private static void AppendSelected(StringBuilder sb, bool selected, string name)
        {
            if (!selected) return;
            if (sb.Length > 0) sb.Append('|');
            sb.Append(name);
        }

        private static string Field(bool include, string value)
        {
            return include ? value : string.Empty;
        }

        public void Dispose()
        {
            StopLocalLoops();
        }
    }
}
