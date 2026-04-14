using System;

namespace PcHost.Core
{
    public static class RingBufferReader
    {
        public static byte[] ReadNewBytes(
            AdsPlcClient client,
            string headSymbol,
            string bufferSymbol,
            int bufferByteSize,
            RingBufferCursor cursor)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(headSymbol)) throw new ArgumentException("Head symbol is required.", nameof(headSymbol));
            if (string.IsNullOrWhiteSpace(bufferSymbol)) throw new ArgumentException("Buffer symbol is required.", nameof(bufferSymbol));
            if (bufferByteSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferByteSize));
            if (cursor == null) throw new ArgumentNullException(nameof(cursor));

            uint head = client.ReadSymbol<uint>(headSymbol);
            uint tail = cursor.NextIndex;

            if (head == tail)
            {
                return Array.Empty<byte>();
            }

            uint headMod = head % (uint)bufferByteSize;
            uint tailMod = tail % (uint)bufferByteSize;

            byte[] buffer = client.ReadBytes(bufferSymbol, bufferByteSize);

            byte[] outBytes;
            if (headMod > tailMod)
            {
                int len = (int)(headMod - tailMod);
                outBytes = new byte[len];
                Buffer.BlockCopy(buffer, (int)tailMod, outBytes, 0, len);
            }
            else
            {
                int part1 = (int)((uint)bufferByteSize - tailMod);
                int part2 = (int)headMod;
                outBytes = new byte[part1 + part2];
                Buffer.BlockCopy(buffer, (int)tailMod, outBytes, 0, part1);
                if (part2 > 0)
                {
                    Buffer.BlockCopy(buffer, 0, outBytes, part1, part2);
                }
            }

            cursor.AdvanceTo(head);
            return outBytes;
        }
    }
}

