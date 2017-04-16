using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;
using System.Runtime.InteropServices;

namespace NativeIOCP
{
    public class Listener
    {
        static int SocketAddressSize = Marshal.SizeOf<SocketAddress>();
        
        private SocketAddress _address;
        private Socket _socket;
        private IoHandle _io;
        private AcceptEx _acceptFn;

        private Listener() { }

        public static Listener OnAddress(System.Net.IPEndPoint listenOn)
        {
            var listenOnBytes = listenOn.Address.MapToIPv4().GetAddressBytes();

            ushort port = (ushort)listenOn.Port;

            var inAddress = new Ipv4InternetAddress();
            inAddress.Byte1 = listenOnBytes[0];
            inAddress.Byte2 = listenOnBytes[1];
            inAddress.Byte3 = listenOnBytes[2];
            inAddress.Byte4 = listenOnBytes[3];

            var sa = new SocketAddress();
            sa.Family = AddressFamilies.Internet;
            sa.Port = WinsockImports.htons(port);
            sa.IpAddress = inAddress;

            var version = new Winsock.Version(2, 2);
            WindowsSocketsData wsaData;
            var startupResult = WinsockImports.WSAStartup((short)version.Raw, out wsaData);

            if (startupResult != System.Net.Sockets.SocketError.Success)
            {
                throw new Exception("Startup failed");
            }

            Socket listenSocket = SocketFactory.Alloc();

            AcceptEx acceptFn = WinsockImports.Initalize(listenSocket);

            var listener = new Listener();
            listener._address = sa;
            listener._acceptFn = acceptFn;
            listener._socket = listenSocket;
            listener._io = IoHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, listenSocket, listener.OnAccept);

            return listener;
        }

        public void Listen()
        {
            var bindResult = WinsockImports.bind(_socket, ref _address, SocketAddressSize);
            if (bindResult == WinsockImports.SocketError)
            {
                throw new Exception("bind failed");
            }
            
            WinsockImports.listen(_socket, 0);

            AcceptNextConnection();
        }

        public void Wait()
        {
            _io.Wait(false);
        }

        private void OnAccept(CallbackInstance callbackInstance, IntPtr context, Overlapped overlapped, uint ioResult, uint bytesTransfered, IoHandle io)
        {
            if (ioResult != WinsockImports.Success)
            {
                _io.Cancel();
                throw new Exception($"accept failed: {ioResult}");
            }

            var connection = overlapped.AcceptedConnection();

            connection.OnAccept(overlapped);
            
            AcceptNextConnection();
        }

        private void AcceptNextConnection()
        {
            var acceptSocket = SocketFactory.Alloc();
            var acceptBuffer = Buf.Alloc(1024);
            var overlapped = OverlappedFactory.Alloc(acceptSocket, acceptBuffer);

            _io.Start();

            uint received;
            var acceptResult = _acceptFn(
                _socket,
                acceptSocket,
                acceptBuffer.Pointer, 0,
                (uint)SocketAddressSize + 16,
                (uint)SocketAddressSize + 16,
                out received,
                overlapped
            );

            if (!acceptResult)
            {
                var error = WinsockImports.WSAGetLastError();

                if (error != WinsockImports.IOPending)
                {
                    _io.Cancel();
                    throw new Exception($"accept failed: {error}");
                }
            }
        }
    }
}
