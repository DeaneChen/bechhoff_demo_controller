using System;

namespace PcHost.Core
{
    public sealed class RingBufferCursor
    {
        public RingBufferCursor(uint nextIndex)
        {
            NextIndex = nextIndex;
        }

        public uint NextIndex { get; private set; }

        public void AdvanceTo(uint nextIndex)
        {
            NextIndex = nextIndex;
        }
    }
}

