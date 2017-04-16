using System;
using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    // TODO: Replace with proper buffer
    struct Buf
    {
        private GCHandle _handle;
        private int _length;

        public int Length => _length;
        public IntPtr Pointer => _handle.AddrOfPinnedObject();

        public static Buf Alloc(int length)
        {
            return Alloc(new byte[length]);
        }

        public static Buf Alloc(byte[] data)
        {
            var length = data.Length;
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            return new Buf
            {
                _handle = handle,
                _length = length
            };
        }

        public void Free()
        {
            _handle.Free();
        }

        public string ToString(int written)
        {
            return Marshal.PtrToStringUTF8(Pointer, written);
        }

        public override string ToString()
        {
            return ToString(Length);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WSABuf
    {
        private uint _length;
        private IntPtr _buf;

        public static WSABuf FromBuf(Buf buf)
        {
            return new WSABuf
            {
                _length = (uint)buf.Length,
                _buf = buf.Pointer
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WSABufs
    {
        private IntPtr _value;

        public static WSABufs Alloc(Buf buf)
        {
            var handle = GCHandle.Alloc(WSABuf.FromBuf(buf), GCHandleType.Pinned);

            return new WSABufs
            {
                _value = handle.AddrOfPinnedObject()
            };
        }
    }
}
