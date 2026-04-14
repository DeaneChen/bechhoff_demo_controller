using System;

namespace PcHost.Core
{
    public sealed class PlcConnectionSettings
    {
        public PlcConnectionSettings(string amsNetId, int port)
        {
            if (string.IsNullOrWhiteSpace(amsNetId))
            {
                throw new ArgumentException("AMS NetId is required.", nameof(amsNetId));
            }

            AmsNetId = amsNetId.Trim();
            Port = port;
        }

        public string AmsNetId { get; }

        public int Port { get; }
    }
}

