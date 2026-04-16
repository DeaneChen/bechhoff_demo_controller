using System;
using System.Threading;
using PcHostGUI.Infrastructure;

namespace PcHostGUI.ViewModels
{
    public sealed class Rs485SensorViewModel : ObservableObject
    {
        private readonly Action<string, string> _log;
        private readonly CancellationToken _appToken;

        private string _status = "Not implemented (PLC-side protocol pending)";
        private byte _channel = 1;
        private byte _slaveId = 1;

        public Rs485SensorViewModel(string name, Action<string, string> log, CancellationToken appToken)
        {
            Name = name ?? "RS485";
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _appToken = appToken;
        }

        public string Name { get; }

        public byte Channel
        {
            get => _channel;
            set => SetProperty(ref _channel, value);
        }

        public byte SlaveId
        {
            get => _slaveId;
            set => SetProperty(ref _slaveId, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}

