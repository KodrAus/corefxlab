using System;
using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativeConnectionOverlapped
    {
        private System.Threading.NativeOverlapped _overlapped;
        private GCHandle _connectionHandle;

        public static NativeConnectionOverlapped ForConnection(GCHandle connectionHandle)
        {
            return new NativeConnectionOverlapped { _connectionHandle = connectionHandle };
        }

        public Connection Connection()
        {
            return (Connection)_connectionHandle.Target;
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

        public static ConnectionOverlapped FromNativeOverlapped(GCHandle nativeOverlapped)
        {
            return new ConnectionOverlapped { _value = nativeOverlapped.AddrOfPinnedObject() };
        }

        public Connection Connection()
        {
            return Marshal.PtrToStructure<NativeConnectionOverlapped>(_value).Connection();
        }
    }

    struct ConnectionOverlappedHandle
    {
        private GCHandle _handle;
        public ConnectionOverlapped Overlapped()
        {
            return ConnectionOverlapped.FromNativeOverlapped(_handle);
        }

        public static ConnectionOverlappedHandle CreateConnection(Socket acceptSocket, Buf requestBuffer, Buf responseBuffer)
        {
            var connection = new Connection(acceptSocket, requestBuffer, responseBuffer);
            var connectionHandle = GCHandle.Alloc(connection);
            var overlappedHandle = GCHandle.Alloc(NativeConnectionOverlapped.ForConnection(connectionHandle), GCHandleType.Pinned);

            var overlapped = new ConnectionOverlappedHandle { _handle = overlappedHandle };

            connection.OnFree(() => overlapped.Free());

            return overlapped;
        }

        public void Free()
        {
            ((NativeConnectionOverlapped)_handle.Target).Free();
            _handle.Free();
        }
    }
}
