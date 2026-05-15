using System;

namespace PcHost.Core
{
    public readonly struct VariableBladeLogRecord
    {
        public const uint Magic = 0x56424D31; // 'VBM1'
        public const int SizeBytes = 54;

        public VariableBladeLogRecord(
            uint seq,
            uint timeUs,
            short pressureRaw,
            short torqueRaw,
            int pd33AbsRaw,
            int pd33RelRaw,
            short vibAccXRaw,
            short vibAccYRaw,
            short vibAccZRaw,
            short vibVelXRaw,
            short vibVelYRaw,
            short vibVelZRaw,
            short vibTempRaw,
            short vibDispXRaw,
            short vibDispYRaw,
            short vibDispZRaw,
            short vibFreqXRaw,
            short vibFreqYRaw,
            short vibFreqZRaw,
            byte valvesBits,
            byte flags)
        {
            Seq = seq;
            TimeUs = timeUs;
            PressureRaw = pressureRaw;
            TorqueRaw = torqueRaw;
            Pd33AbsRaw = pd33AbsRaw;
            Pd33RelRaw = pd33RelRaw;
            VibAccXRaw = vibAccXRaw;
            VibAccYRaw = vibAccYRaw;
            VibAccZRaw = vibAccZRaw;
            VibVelXRaw = vibVelXRaw;
            VibVelYRaw = vibVelYRaw;
            VibVelZRaw = vibVelZRaw;
            VibTempRaw = vibTempRaw;
            VibDispXRaw = vibDispXRaw;
            VibDispYRaw = vibDispYRaw;
            VibDispZRaw = vibDispZRaw;
            VibFreqXRaw = vibFreqXRaw;
            VibFreqYRaw = vibFreqYRaw;
            VibFreqZRaw = vibFreqZRaw;
            ValvesBits = valvesBits;
            Flags = flags;
        }

        public uint Seq { get; }
        public uint TimeUs { get; }
        public short PressureRaw { get; }
        public short TorqueRaw { get; }
        public int Pd33AbsRaw { get; }
        public int Pd33RelRaw { get; }
        public short VibAccXRaw { get; }
        public short VibAccYRaw { get; }
        public short VibAccZRaw { get; }
        public short VibVelXRaw { get; }
        public short VibVelYRaw { get; }
        public short VibVelZRaw { get; }
        public short VibTempRaw { get; }
        public short VibDispXRaw { get; }
        public short VibDispYRaw { get; }
        public short VibDispZRaw { get; }
        public short VibFreqXRaw { get; }
        public short VibFreqYRaw { get; }
        public short VibFreqZRaw { get; }
        public byte ValvesBits { get; }
        public byte Flags { get; }

        public double VibAccX => VibAccXRaw / 32768.0 * 16.0;
        public double VibAccY => VibAccYRaw / 32768.0 * 16.0;
        public double VibAccZ => VibAccZRaw / 32768.0 * 16.0;
        public double VibVelX => VibVelXRaw / 100.0;
        public double VibVelY => VibVelYRaw / 100.0;
        public double VibVelZ => VibVelZRaw / 100.0;
        public double VibTempC => VibTempRaw / 100.0;
        public double VibDispX => VibDispXRaw;
        public double VibDispY => VibDispYRaw;
        public double VibDispZ => VibDispZRaw;
        public double VibFreqX => VibFreqXRaw / 10.0;
        public double VibFreqY => VibFreqYRaw / 10.0;
        public double VibFreqZ => VibFreqZRaw / 10.0;

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
            short vibAccX = ReadI16Le(bytes, 24);
            short vibAccY = ReadI16Le(bytes, 26);
            short vibAccZ = ReadI16Le(bytes, 28);
            short vibVelX = ReadI16Le(bytes, 30);
            short vibVelY = ReadI16Le(bytes, 32);
            short vibVelZ = ReadI16Le(bytes, 34);
            short vibTemp = ReadI16Le(bytes, 36);
            short vibDispX = ReadI16Le(bytes, 38);
            short vibDispY = ReadI16Le(bytes, 40);
            short vibDispZ = ReadI16Le(bytes, 42);
            short vibFreqX = ReadI16Le(bytes, 44);
            short vibFreqY = ReadI16Le(bytes, 46);
            short vibFreqZ = ReadI16Le(bytes, 48);
            byte valves = bytes[50];
            byte flags = bytes[51];

            record = new VariableBladeLogRecord(
                seq,
                tUs,
                pressure,
                torque,
                absRaw,
                relRaw,
                vibAccX,
                vibAccY,
                vibAccZ,
                vibVelX,
                vibVelY,
                vibVelZ,
                vibTemp,
                vibDispX,
                vibDispY,
                vibDispZ,
                vibFreqX,
                vibFreqY,
                vibFreqZ,
                valves,
                flags);
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
