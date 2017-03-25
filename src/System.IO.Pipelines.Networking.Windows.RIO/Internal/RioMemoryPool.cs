using System.Buffers;
using System.IO.Pipelines.Networking.Windows.RIO.Internal.Winsock;

namespace System.IO.Pipelines.Networking.Windows.RIO.Internal
{
    internal class RioMemoryPool : MemoryPool
    {
        private readonly RegisteredIO _rio;

        public RioMemoryPool(RegisteredIO rio)
        {
            _rio = rio;
        }

        protected override MemoryPoolSlab CreateSlab(int length)
        {
            var slab = RioMemoryPoolSlab.Create(length);
            slab.Register(_rio);
            
            return slab;
        }

        protected override MemoryPoolBlock CreateBlock(int offset, int length, MemoryPoolSlab slab)
        {
            return RioMemoryPoolBlock.Create(offset, length, this, slab as RioMemoryPoolSlab);
        }
    }

    internal class RioMemoryPoolSlab : MemoryPoolSlab
    {
        private RioMemoryPoolSlab(byte[] data) : base(data)
        {
            
        }
        
        public static new RioMemoryPoolSlab Create(int length)
        {
            // allocate and pin requested memory length
            var array = new byte[length];

            // allocate and return slab tracking object
            return new RioMemoryPoolSlab(array);
        }

        public void Register(RegisteredIO rio)
        {
            _bufferId = rio.RioRegisterBuffer(NativePointer, (uint)Length);
        }

        private IntPtr _bufferId;
        public IntPtr BufferId => _bufferId;
    }

    internal class RioMemoryMetadata : IMemoryMetadata
    {
        public RioMemoryMetadata(IntPtr bufferId, long bufferStartAddress)
        {
            _bufferId = bufferId;
            _bufferStartAddress = bufferStartAddress;
        }

        private IntPtr _bufferId;
        public IntPtr BufferId => _bufferId;

        private long _bufferStartAddress;
        public long BufferStartAddress => _bufferStartAddress;
    }

    internal class RioMemoryPoolBlock : MemoryPoolBlock
    {
        public RioMemoryPoolBlock(RioMemoryPool pool, RioMemoryPoolSlab slab, int offset, int length) : base(pool, slab, offset, length)
        {
        }

        internal static RioMemoryPoolBlock Create(
            int offset,
            int length,
            RioMemoryPool pool,
            RioMemoryPoolSlab slab)
        {
            return new RioMemoryPoolBlock(pool, slab, offset, length)
            {
                _metadata = new RioMemoryMetadata(slab.BufferId, slab.NativePointer.ToInt64())
#if BLOCK_LEASE_TRACKING
                Leaser = Environment.StackTrace,
#endif
            };
        }

        public new RioMemoryPoolSlab Slab { get; }

        private RioMemoryMetadata _metadata;
        public override IMemoryMetadata Metadata => _metadata;
    }
}
