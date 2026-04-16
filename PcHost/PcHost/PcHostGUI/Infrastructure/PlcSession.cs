using System;
using System.Threading;
using System.Threading.Tasks;
using PcHost.Core;

namespace PcHostGUI.Infrastructure
{
    public sealed class PlcSession : IDisposable
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private AdsPlcClient _client;
        private PlcConnectionSettings _settings;

        public bool IsConnected => _client != null && _client.IsConnected;

        public void Connect(PlcConnectionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Disconnect();

            _client = new AdsPlcClient();
            _client.Connect(settings);
            _settings = settings;
        }

        public void Disconnect()
        {
            _settings = null;
            if (_client != null)
            {
                try { _client.Dispose(); } catch { }
                _client = null;
            }
        }

        public async Task<T> ReadAsync<T>(string symbol, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol is required.", nameof(symbol));
            ct.ThrowIfCancellationRequested();

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                EnsureConnected();
                return _client.ReadSymbol<T>(symbol);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task WriteAsync<T>(string symbol, T value, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol is required.", nameof(symbol));
            ct.ThrowIfCancellationRequested();

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                EnsureConnected();
                _client.WriteSymbol(symbol, value);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<TResult> ExecuteAsync<TResult>(Func<AdsPlcClient, TResult> func, CancellationToken ct)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            ct.ThrowIfCancellationRequested();

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                EnsureConnected();
                return func(_client);
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("PLC not connected.");
            }
        }

        public void Dispose()
        {
            Disconnect();
            _gate.Dispose();
        }
    }
}
