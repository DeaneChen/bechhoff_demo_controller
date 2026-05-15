using System;

namespace PcHost.Core
{
    public readonly struct VariableBladeLogRecord
    {
        public const uint Magic = 0x56424D31; // 'VBM1'
        public const int SizeBytes = 28;

        public VariableBladeLogRecord(
            uint seq,
            uint timeUs,
            short pressureRaw,
            short torqueRaw,
            int pd33AbsRaw,
            int pd33RelRaw,
            byte valvesBits,
            byte flags)
        {
            Seq = seq;
            TimeUs = timeUs;
            PressureRaw = pressureRaw;
            TorqueRaw = torqueRaw;
            Pd33AbsRaw = pd33AbsRaw;
            Pd33RelRaw = pd33RelRaw;
            ValvesBits = valvesBits;
            Flags = flags;
        }

        public uint Seq { get; }
        public uint TimeUs { get; }
        public short PressureRaw { get; }
        public short TorqueRaw { get; }
        public int Pd33AbsRaw { get; }
        public int Pd33RelRaw { get; }
        public byte ValvesBits { get; }
        public byte Flags { get; }

        public static bool TryParse(ReadOnlySpan<byte> bytes, out VariableBladeLogRecord record)
        {
            record = default;
            if (bytes.Length < SizeBytes) return false;

            uint magic = ReadU32Le(bytes, 0);
            if (magic != Magic) return false;

            uint seq = ReadU32Le(bytes, 4);
            uint tUs = ReadU32Le(bytes, 8);
            short pressure = ReadI16Le(bytes, 12);
            short torque = ReadI16Le(bytes, 14);
            int absRaw = ReadI32Le(bytes, 16);
            int relRaw = ReadI32Le(bytes, 20);
            byte valves = bytes[24];
            byte flags = bytes[25];

            record = new VariableBladeLogRecord(seq, tUs, pressure, torque, absRaw, relRaw, valves, flags);
            return true;
        }

        private static uint ReadU32Le(ReadOnlySpan<byte> s, int o)
        {
            return (uint)(s[o] | (s[o + 1] << 8) | (s[o + 2] << 16) | (s[o + 3] << 24));
        }

        private static int ReadI32Le(ReadOnlySpan<byte> s, int o)
        {
            unchecked
            {
                return (int)ReadU32Le(s, o);
            }
        }

        private static short ReadI16Le(ReadOnlySpan<byte> s, int o)
        {
            unchecked
            {
                return (short)(s[o] | (s[o + 1] << 8));
            }
        }
    }
}

