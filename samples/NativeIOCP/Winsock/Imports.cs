using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    [StructLayout(LayoutKind.Sequential)]
    struct WindowsSocketsData
    {
        internal short Version;
        internal short HighVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        internal string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        internal string SystemStatus;
        internal short MaxSockets;
        internal short MaxDatagramSize;
        internal IntPtr VendorInfo;
    }
    
    struct Version
    {
        public ushort Raw;

        public Version(byte major, byte minor)
        {
            Raw = major;
            Raw <<= 8;
            Raw += minor;
        }

        public byte Major
        {
            get
            {
                ushort result = Raw;
                result >>= 8;
                return (byte)result;
            }
        }

        public byte Minor
        {
            get
            {
                ushort result = Raw;
                result &= 0x00FF;
                return (byte)result;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}", Major, Minor);
        }
    }
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    unsafe delegate bool AcceptEx(
        [In] Socket listenSocket,
        [In] Socket acceptSocket,
        [In] IntPtr outputBuffer,
        [In] uint receiveBufferLength,
        [In] uint localAddressLength,
        [In] uint remoteAddressLength,
        [Out] out uint bytesReceived,
        [In] ConnectionOverlapped overlapped
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    unsafe delegate bool OverlappedCompletionRoutine(
        [In] uint error,
        [In] uint transfered,
        [In] Overlapped overlapped,
        [In] IntPtr flags
    );

    static class WinsockImports
    {
        const string Ws232 = "WS2_32.dll";
        
        const uint IocOut = 0x40000000;
        const uint IocIn = 0x80000000;
        const uint IOC_INOUT = IocIn | IocOut;
        const uint IocWs2 = 0x08000000;
        const uint IocVendor = 0x18000000;
        const uint SioGetExtensionFunctionPointer = IOC_INOUT | IocWs2 | 6;

        const int SioLoopbackFastPath = -1744830448;// IOC_IN | IOC_WS2 | 16;

        const int TcpNodelay = 0x0001;
        const int IPPROTO_TCP = 6;

        public unsafe static AcceptEx Initalize(Socket socket)
        {
            UInt32 dwBytes = 0;
            IntPtr acceptExPtr = IntPtr.Zero;
            Guid acceptExId = new Guid("b5367df1-cbac-11cf-95ca-00805f48a192");

            int True = -1;

            int result = setsockopt(socket, IPPROTO_TCP, TcpNodelay, (char*)&True, 4);
            if (result != 0)
            {
                var error = WSAGetLastError();
                WSACleanup();
                throw new Exception($"ERROR: setsockopt TCP_NODELAY returned {error}");
            }
            
            result = WSAIoctl(socket, SioGetExtensionFunctionPointer,
               ref acceptExId, 16, ref acceptExPtr,
               sizeof(IntPtr),
               out dwBytes, IntPtr.Zero, IntPtr.Zero);

            if (result != 0)
            {
                var error = WSAGetLastError();
                WSACleanup();
                throw new Exception($"ERROR: Initalize returned {error}");
            }
            
            var acceptEx = Marshal.GetDelegateForFunctionPointer<AcceptEx>(acceptExPtr);
            
            return acceptEx;
        }

        public static void Startup()
        {
            var version = new Version(2, 2);
            var startupResult = WSAStartup((short)version.Raw, out WindowsSocketsData wsaData);

            if (startupResult != System.Net.Sockets.SocketError.Success)
            {
                throw new Exception("Startup failed");
            }
        }

        public static GCHandle ReadFlags = GCHandle.Alloc(0, GCHandleType.Pinned);

        [DllImport(Ws232, SetLastError = true)]
        private static extern int WSAIoctl(
          [In] Socket socket,
          [In] uint dwIoControlCode,
          [In] ref Guid lpvInBuffer,
          [In] uint cbInBuffer,
          [In, Out] ref IntPtr lpvOutBuffer,
          [In] int cbOutBuffer,
          [Out] out uint lpcbBytesReturned,
          [In] IntPtr lpOverlapped,
          [In] IntPtr lpCompletionRoutine
        );
        
        [DllImport(Ws232, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = true, ThrowOnUnmappableChar = true)]
        internal static extern SocketError WSAStartup([In] short wVersionRequested, [Out] out WindowsSocketsData lpWindowsSocketsData);

        [DllImport(Ws232, SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern Socket WSASocket([In] AddressFamilies af, [In] SocketType type, [In] Protocol protocol, [In] IntPtr lpProtocolInfo, [In] Int32 group, [In] SocketFlags dwFlags);

        [DllImport(Ws232, SetLastError = true)]
        public static extern int WSARecv([In] Socket socket, [In, Out] WSABufs buffers, [In] uint bufferCount, [Out] out uint numberOfBytesRecvd, [In, Out] IntPtr flags, [In] Overlapped overlapped, [In] OverlappedCompletionRoutine completionRoutine);

        [DllImport(Ws232, SetLastError = true)]
        public static extern int WSASend([In] Socket socket, [In] WSABufs buffers, [In] uint bufferCount, [Out] out uint numberOfBytesSent, [In] uint flags, [In] Overlapped overlapped, [In] OverlappedCompletionRoutine completionRoutine);

        [DllImport(Ws232, SetLastError = true)]
        public static extern ushort htons([In] ushort hostshort);

        [DllImport(Ws232, SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern int bind(Socket s, ref SocketAddress name, int namelen);

        [DllImport(Ws232, SetLastError = true)]
        public static extern int listen(Socket s, int backlog);

        [DllImport(Ws232, SetLastError = true)]
        public unsafe static extern int setsockopt(Socket s, int level, int optname, char* optval, int optlen);
        
        [DllImport(Ws232)]
        public static extern Int32 WSAGetLastError();

        [DllImport(Ws232, SetLastError = true)]
        public static extern Int32 WSACleanup();

        [DllImport(Ws232, SetLastError = true)]
        public static extern int closesocket(Socket s);

        public const int SocketError = -1;
        public const int InvalidSocket = -1;
        public const int Success = 0;
        public const int IOPending = 997;
    }
}
