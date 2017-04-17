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

        public static NativeConnectionOverlapped ForConnection(Connection connection)
        {
            var connectionHandle = GCHandle.Alloc(connection);
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

        public static ConnectionOverlapped ForNewConnection(Socket acceptSocket, Buf requestBuffer, Buf responseBuffer)
        {
            var connection = new Connection(acceptSocket, requestBuffer, responseBuffer);
            
            var overlappedHandle = GCHandle.Alloc(NativeConnectionOverlapped.ForConnection(connection), GCHandleType.Pinned);

            connection.SetFreeCallback(() =>
            {
                ((NativeConnectionOverlapped)overlappedHandle.Target).Free();
                overlappedHandle.Free();
            });

            return new ConnectionOverlapped { _value = overlappedHandle.AddrOfPinnedObject() };
        }
    }
}
