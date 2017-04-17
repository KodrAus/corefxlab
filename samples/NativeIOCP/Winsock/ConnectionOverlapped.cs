using System;
using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativeConnectionOverlapped
    {
        private System.Threading.NativeOverlapped _overlapped;
        private GCHandle _connectionHandle;

        public Connection Connection => (Connection)_connectionHandle.Target;

        public static NativeConnectionOverlapped ForConnection(GCHandle connectionHandle)
        {
            return new NativeConnectionOverlapped { _connectionHandle = connectionHandle };
        }
        
        public void Free()
        {
            _connectionHandle.Free();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ConnectionOverlapped
    {
        private IntPtr _value;

        public Connection Connection => Marshal.PtrToStructure<NativeConnectionOverlapped>(_value).Connection;

        public static ConnectionOverlapped FromNativeOverlapped(GCHandle nativeOverlapped)
        {
            return new ConnectionOverlapped { _value = nativeOverlapped.AddrOfPinnedObject() };
        }
    }

    struct ConnectionOverlappedHandle
    {
        private GCHandle _handle;

        public ConnectionOverlapped Overlapped => ConnectionOverlapped.FromNativeOverlapped(_handle);

        public static ConnectionOverlappedHandle CreateConnection(Socket acceptSocket, Buf requestBuffer, Buf responseBuffer)
        {
            var connection = new Connection(acceptSocket, requestBuffer, responseBuffer);

            var connectionHandle = GCHandle.Alloc(connection);
            var overlappedHandle = GCHandle.Alloc(NativeConnectionOverlapped.ForConnection(connectionHandle), GCHandleType.Pinned);
            var overlapped = new ConnectionOverlappedHandle { _handle = overlappedHandle };

            connection.SetFreeCallback(() => overlapped.Free());

            return overlapped;
        }

        public void Free()
        {
            ((NativeConnectionOverlapped)_handle.Target).Free();
            _handle.Free();
        }
    }
}
