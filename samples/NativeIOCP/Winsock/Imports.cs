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

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct SocketAddress
    {
        public AddressFamilies Family;
        public ushort Port;
        public Ipv4InternetAddress IpAddress;
        public fixed byte Padding[8];

        public static SocketAddress FromIPEndPoint(System.Net.IPEndPoint ipEndPoint)
        {
            var listenOnBytes = ipEndPoint.Address.MapToIPv4().GetAddressBytes();

            ushort port = (ushort)ipEndPoint.Port;

            var inAddress = new Ipv4InternetAddress();
            inAddress.Byte1 = listenOnBytes[0];
            inAddress.Byte2 = listenOnBytes[1];
            inAddress.Byte3 = listenOnBytes[2];
            inAddress.Byte4 = listenOnBytes[3];

            var sa = new SocketAddress();
            sa.Family = AddressFamilies.Internet;
            sa.Port = WinsockImports.htons(port);
            sa.IpAddress = inAddress;

            return sa;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    struct Ipv4InternetAddress
    {
        [FieldOffset(0)]
        public byte Byte1;
        [FieldOffset(1)]
        public byte Byte2;
        [FieldOffset(2)]
        public byte Byte3;
        [FieldOffset(3)]
        public byte Byte4;
    }

    enum Protocol : short
    {
        IpProtocolTcp = 6,
    }

    enum SocketType : short
    {
        Stream = 1,
    }

    enum AddressFamilies : short
    {
        Internet = 2,
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

    [StructLayout(LayoutKind.Sequential)]
    struct Socket
    {
        private IntPtr _value;

        public bool IsValid()
        {
            return _value != IntPtr.Zero;
        }
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

    [StructLayout(LayoutKind.Sequential)]
    struct NativeOverlapped
    {
        private System.Threading.NativeOverlapped _overlapped;
        private GCHandle _connectionHandle;

        public static NativeOverlapped ForConnection(GCHandle connectionHandle)
        {
            return new NativeOverlapped { _connectionHandle = connectionHandle };
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
    struct Overlapped
    {
        private IntPtr _value;

        public static Overlapped FromNativeOverlapped(GCHandle nativeOverlapped)
        {
            return new Overlapped { _value = nativeOverlapped.AddrOfPinnedObject() };
        }

        public Connection Connection()
        {
            return Marshal.PtrToStructure<NativeOverlapped>(_value).Connection();
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }

    struct OverlappedHandle
    {
        private GCHandle _handle;
        public Overlapped Value()
        {
            return Overlapped.FromNativeOverlapped(_handle);
        }

        public static OverlappedHandle Alloc(Listener listener, Socket acceptSocket, Buf acceptBuffer)
        {
            var connectionHandle = GCHandle.Alloc(new Connection(listener, acceptSocket, acceptBuffer));
            var overlappedHandle = GCHandle.Alloc(NativeOverlapped.ForConnection(connectionHandle), GCHandleType.Pinned);

            return new OverlappedHandle { _handle = overlappedHandle };
        }

        public void Free()
        {
            ((NativeOverlapped)_handle.Target).Free();
            _handle.Free();
        }
    }
    
    // TODO: Replace with proper buffer
    struct Buf
    {
        private IntPtr _data;
        private int _length;

        public int Length => _length;
        public IntPtr Pointer => _data;
        
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
                _data = handle.AddrOfPinnedObject(),
                _length = length
            };
        }

        public string AsString(int written)
        {
            return Marshal.PtrToStringUTF8(_data, written);
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
        
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    unsafe delegate bool AcceptEx(
        [In] Socket listenSocket,
        [In] Socket acceptSocket,
        [In] IntPtr outputBuffer,
        [In] uint receiveBufferLength,
        [In] uint localAddressLength,
        [In] uint remoteAddressLength,
        [Out] out uint bytesReceived,
        [In] Overlapped overlapped
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
