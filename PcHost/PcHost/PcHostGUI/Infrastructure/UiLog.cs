using System;

namespace PcHostGUI.Infrastructure
{
    public sealed class UiLog
    {
        public UiLog(DateTime utc, string level, string message)
        {
            Utc = utc;
            Level = level ?? "INFO";
            Message = message ?? string.Empty;
        }

        public DateTime Utc { get; }
        public string Level { get; }
        public string Message { get; }
    }
}

