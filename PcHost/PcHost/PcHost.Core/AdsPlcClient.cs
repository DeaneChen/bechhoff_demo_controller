using System;
using System.Collections.Generic;
using System.IO;
using TwinCAT.Ads;

namespace PcHost.Core
{
    public sealed class AdsPlcClient : IDisposable
    {
        private readonly AdsClient _client = new AdsClient();
        private readonly Dictionary<string, uint> _handleCache = new Dictionary<string, uint>(StringComparer.Ordinal);
        private bool _connected;

        public bool IsConnected => _connected;

        public void Connect(PlcConnectionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _client.Connect(settings.AmsNetId, settings.Port);
            _connected = true;
        }

        public T ReadSymbol<T>(string symbolName)
        {
            if (string.IsNullOrWhiteSpace(symbolName)) throw new ArgumentException("Symbol name is required.", nameof(symbolName));
            EnsureConnected();

            uint handle = GetOrCreateHandle(symbolName);
            object value = _client.ReadAny(handle, typeof(T));
            return (T)value;
        }

        public void WriteSymbol<T>(string symbolName, T value)
        {
            if (string.IsNullOrWhiteSpace(symbolName)) throw new ArgumentException("Symbol name is required.", nameof(symbolName));
            EnsureConnected();

            uint handle = GetOrCreateHandle(symbolName);
            _client.WriteAny(handle, value);
        }

        public byte[] ReadBytes(string symbolName, int byteCount)
        {
            if (string.IsNullOrWhiteSpace(symbolName)) throw new ArgumentException("Symbol name is required.", nameof(symbolName));
            if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
            EnsureConnected();

            uint handle = GetOrCreateHandle(symbolName);
            var buffer = new byte[byteCount];
            _client.Read(handle, new Memory<byte>(buffer));
            return buffer;
        }

        public void WriteBytes(string symbolName, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(symbolName)) throw new ArgumentException("Symbol name is required.", nameof(symbolName));
            if (data == null) throw new ArgumentNullException(nameof(data));
            EnsureConnected();

            uint handle = GetOrCreateHandle(symbolName);
            _client.Write(handle, new ReadOnlyMemory<byte>(data));
        }

        public void Dispose()
        {
            foreach (var kv in _handleCache)
            {
                try
                {
                    _client.DeleteVariableHandle(kv.Value);
                }
                catch
                {
                    // ignore
                }
            }
            _handleCache.Clear();

            try
            {
                _client.Dispose();
            }
            catch
            {
                // ignore
            }

            _connected = false;
        }

        private uint GetOrCreateHandle(string symbolName)
        {
            if (_handleCache.TryGetValue(symbolName, out uint handle))
            {
                return handle;
            }

            uint created = _client.CreateVariableHandle(symbolName);
            _handleCache[symbolName] = created;
            return created;
        }

        private void EnsureConnected()
        {
            if (!_connected)
            {
                throw new InvalidOperationException("Not connected. Call Connect() first.");
            }
        }
    }
}
