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
        private string _status = "Idle";

        private bool _valveEnable;
        private bool _valve1;
        private bool _valve2;
        private bool _valve3;
        private bool _valve4;

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

            ApplyValveCommand = new AsyncRelayCommand(ApplyValveAsync, () => _plc.IsConnected);
            RefreshValveCommand = new AsyncRelayCommand(RefreshValveAsync, () => _plc.IsConnected);

            StartAllCommand = new AsyncRelayCommand(StartAllAsync, () => _plc.IsConnected && _logCts == null);
            StopAllCommand = new AsyncRelayCommand(StopAllAsync, () => _plc.IsConnected && _logCts != null);
            ResetLoggerCommand = new AsyncRelayCommand(ResetLoggerAsync, () => _plc.IsConnected);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public bool ValveEnable
        {
            get => _valveEnable;
            set => SetProperty(ref _valveEnable, value);
        }

        public bool Valve1 { get => _valve1; set => SetProperty(ref _valve1, value); }
        public bool Valve2 { get => _valve2; set => SetProperty(ref _valve2, value); }
        public bool Valve3 { get => _valve3; set => SetProperty(ref _valve3, value); }
        public bool Valve4 { get => _valve4; set => SetProperty(ref _valve4, value); }

        public bool LoggerEnable
        {
            get => _loggerEnable;
            private set => SetProperty(ref _loggerEnable, value);
        }

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
        public AsyncRelayCommand StartAllCommand { get; }
        public AsyncRelayCommand StopAllCommand { get; }
        public AsyncRelayCommand ResetLoggerCommand { get; }

        private async Task ApplyValveAsync()
        {
            var ct = _appToken;
            try
            {
                await _plc.WriteAsync("GVL_ValveIO.Enable", ValveEnable, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve1_Cmd", Valve1, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve2_Cmd", Valve2, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve3_Cmd", Valve3, ct).ConfigureAwait(false);
                await _plc.WriteAsync("GVL_ValveIO.Valve4_Cmd", Valve4, ct).ConfigureAwait(false);
                _log("INFO", "Valve outputs applied.");
                await RefreshValveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log("ERROR", "Apply valves failed: " + ex.GetType().Name + ": " + ex.Message);
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
            catch
            {
                // ignore
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
                _log("INFO", "Logger reset requested.");
            }
            catch (Exception ex)
            {
                _log("ERROR", "Reset logger failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task StartAllAsync()
        {
            var ct = _appToken;
            if (_logCts != null) return;

            try
            {
                Status = "Starting...";

                // Enable PD33 so its values appear in records (no-op if not used)
                await _plc.WriteAsync("GVL_PD33.Enable", true, ct).ConfigureAwait(false);

                // Enable logger
                await _plc.WriteAsync("GVL_DataLogger.Enable", true, ct).ConfigureAwait(false);
                LoggerEnable = true;

                _logCts = CancellationTokenSource.CreateLinkedTokenSource(_appToken);
                _ = Task.Run(() => LogLoopAsync(_logCts.Token), _logCts.Token);

                Status = "Logging";
                _log("INFO", "Started all acquisition + logging.");
            }
            catch (Exception ex)
            {
                Status = "Start failed";
                _log("ERROR", "StartAll failed: " + ex.GetType().Name + ": " + ex.Message);
                try { _logCts?.Cancel(); } catch { }
                try { _logCts?.Dispose(); } catch { }
                _logCts = null;
            }
        }

        private async Task StopAllAsync()
        {
            var ct = _appToken;
            if (_logCts == null) return;

            Status = "Stopping...";
            try { _logCts.Cancel(); } catch { }
            try { _logCts.Dispose(); } catch { }
            _logCts = null;

            try
            {
                await _plc.WriteAsync("GVL_DataLogger.Enable", false, ct).ConfigureAwait(false);
            }
            catch { }

            LoggerEnable = false;
            Status = "Stopped";
            _log("INFO", "Stopped logging.");
        }

        private async Task LogLoopAsync(CancellationToken ct)
        {
            const string headSymbol = "GVL_DataLogger.Head";
            const string tailSymbol = "GVL_DataLogger.Tail";
            const string bufferSymbol = "GVL_DataLogger.Buffer";
            const int bufferSize = 28 * 4096;

            byte[] carry = Array.Empty<byte>();
            var cursor = new RingBufferCursor(0);

            string path = LogFilePath;
            if (string.IsNullOrWhiteSpace(path)) path = "vbm_log.csv";
            path = Path.GetFullPath(path);

            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                writer.WriteLine("pc_utc_iso,seq,t_us,pressure_raw,torque_raw,pd33_abs_raw,pd33_rel_raw,valves_bits,flags");
                writer.Flush();

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        byte[] data = await _plc.ExecuteAsync(c => RingBufferReader.ReadNewBytes(c, headSymbol, bufferSymbol, bufferSize, cursor), ct).ConfigureAwait(false);
                        if (data.Length > 0)
                        {
                            // tell PLC consumed tail
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
                                        rec.PressureRaw.ToString(CultureInfo.InvariantCulture) + "," +
                                        rec.TorqueRaw.ToString(CultureInfo.InvariantCulture) + "," +
                                        rec.Pd33AbsRaw.ToString(CultureInfo.InvariantCulture) + "," +
                                        rec.Pd33RelRaw.ToString(CultureInfo.InvariantCulture) + "," +
                                        rec.ValvesBits.ToString(CultureInfo.InvariantCulture) + "," +
                                        rec.Flags.ToString(CultureInfo.InvariantCulture)
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

                        // update basic stats
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

        public void Dispose()
        {
            try { _logCts?.Cancel(); } catch { }
            try { _logCts?.Dispose(); } catch { }
            _logCts = null;
        }
    }
}
