using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace NativeIOCP
{
    public class Listener
    {
        static int SocketAddressSize = Marshal.SizeOf<SocketAddress>();
        
        private SocketAddress _address;
        private Socket _socket;
        private IoHandle _io;
        private WorkHandle _accept;
        private AcceptEx _acceptFn;

        private ConcurrentDictionary<Socket, OverlappedHandle> _connections;

        private Listener()
        {
            _connections = new ConcurrentDictionary<Socket, OverlappedHandle>();
        }

        public static Listener OnAddress(System.Net.IPEndPoint listenOn)
        {
            var version = new Winsock.Version(2, 2);
            var startupResult = WinsockImports.WSAStartup((short)version.Raw, out WindowsSocketsData wsaData);

            if (startupResult != System.Net.Sockets.SocketError.Success)
            {
                throw new Exception("Startup failed");
            }

            Socket listenSocket = SocketFactory.Alloc();
            AcceptEx acceptFn = WinsockImports.Initalize(listenSocket);
            SocketAddress address = SocketAddress.FromIPEndPoint(listenOn);

            var listener = new Listener();
            listener._address = address;
            listener._acceptFn = acceptFn;
            listener._socket = listenSocket;
            listener._io = IoHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, listenSocket, listener.OnAccept);
            listener._accept = WorkHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, listener.AcceptNextConnection);

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

            _accept.Submit();
        }

        public void Wait()
        {
            _accept.Wait(false);
            _io.Wait(false);
        }

        public void Stop()
        {
            _accept.Wait(true);
            _io.Wait(true);
        }

        internal void Free(Socket socket)
        {
            if (!_connections.TryRemove(socket, out OverlappedHandle overlapped))
            {
                throw new Exception("unknown socket to free");
            }

            overlapped.Free();
        }

        private void OnAccept(CallbackInstance callbackInstance, IntPtr context, Overlapped overlapped, uint ioResult, uint bytesTransfered, IoHandle io)
        {
            if (ioResult != WinsockImports.Success)
            {
                _io.Cancel();
                throw new Exception($"accept failed: {ioResult}");
            }

            _accept.Submit();

            Console.WriteLine(overlapped.ToString());

            overlapped.Connection().OnAccept(overlapped, bytesTransfered);
        }
        
        private void AcceptNextConnection(CallbackInstance callbackInstance, IntPtr context, WorkHandle work)
        {
            var bufferSize = 1024;
            var addressSize = SocketAddressSize + 16;
            var readWriteSize = bufferSize - (addressSize * 2);
            var readSize = readWriteSize / 2;

            var acceptSocket = SocketFactory.Alloc();
            var requestBuffer = Buf.Alloc(bufferSize);

            var body = "Hello, world!";
            var res = $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}";
            var responseBuffer = Buf.Alloc(Encoding.UTF8.GetBytes(res));

            var overlapped = OverlappedHandle.Alloc(this, acceptSocket, requestBuffer, responseBuffer);

            if (_connections.ContainsKey(acceptSocket))
            {
                throw new Exception("Attempted to add the same socket multiple times");
            }
            _connections.GetOrAdd(acceptSocket, overlapped);

            _io.Start();
            var acceptResult = _acceptFn(
                _socket,
                acceptSocket,
                requestBuffer.Pointer, 0,
                (uint)addressSize,
                (uint)addressSize,
                out uint received,
                overlapped.Value()
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
