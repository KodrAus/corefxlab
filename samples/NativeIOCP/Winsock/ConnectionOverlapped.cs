using System;
using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativeConnectionOverlapped
    {
        private System.Threading.NativeOverlapped _overlapped;
        private IntPtr _connectionHandlePointer;

        public Connection Connection => (Connection)GCHandle.FromIntPtr(_connectionHandlePointer).Target;
        
        public static NativeConnectionOverlapped ForConnection(IntPtr connectionHandlePointer)
        {   
            return new NativeConnectionOverlapped { _connectionHandlePointer = connectionHandlePointer };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Overlapped
    {
        private IntPtr _value;

        public static Overlapped FromPointer(IntPtr overlapped)
        {
            return new Overlapped { _value = overlapped };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ConnectionOverlapped
    {
        private IntPtr _value;

        public Connection Connection => Marshal.PtrToStructure<NativeConnectionOverlapped>(_value).Connection;
        public Overlapped Overlapped => Overlapped.FromPointer(_value);

        public static ConnectionOverlapped ForNewConnection(Socket acceptSocket, Buf requestBuffer, Buf responseBuffer)
        {
            var connection = new Connection(acceptSocket, requestBuffer, responseBuffer);
            var connectionHandle = GCHandle.Alloc(connection);

            var overlapped = NativeConnectionOverlapped.ForConnection(GCHandle.ToIntPtr(connectionHandle));
            var overlappedHandle = GCHandle.Alloc(overlapped, GCHandleType.Pinned);

            connection.SetFreeCallback(() =>
            {
                connectionHandle.Free();
                overlappedHandle.Free();
            });

            return new ConnectionOverlapped { _value = overlappedHandle.AddrOfPinnedObject() };
        }

        public void OnAccept(uint transfered)
        {
            Connection.OnAccept(Overlapped, transfered);
        }
    }
}
