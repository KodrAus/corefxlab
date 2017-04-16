using System;
using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    [StructLayout(LayoutKind.Sequential)]
    struct Socket
    {
        private IntPtr _value;

        public bool IsValid()
        {
            return _value != IntPtr.Zero;
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }

    enum Protocol : short
    {
        IpProtocolTcp = 6,
    }

    enum SocketType : short
    {
        Stream = 1,
    }
    
    enum SocketFlags : uint
    {
        Overlapped = 0x01,
        MultipointCRoot = 0x02,
        MultipointCLeaf = 0x04,
        MultipointDRoot = 0x08,
        MultipointDLeaf = 0x10,
        AccessSystemSecurity = 0x40,
        NoHandleInherit = 0x80,
        RegisteredIO = 0x100
    }

    static class SocketFactory
    {
        public static Socket Alloc()
        {
            var socket = WinsockImports.WSASocket(
                AddressFamilies.Internet,
                SocketType.Stream,
                Protocol.IpProtocolTcp,
                IntPtr.Zero,
                0,
                SocketFlags.Overlapped
            );

            if (!socket.IsValid())
            {
                var error = WinsockImports.WSAGetLastError();
                throw new Exception(string.Format("ERROR: WSASocket returned {0}", error));
            }

            return socket;
        }
    }
}
