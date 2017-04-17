using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NativeIOCP
{
    public class Listener
    {
        static int _socketAddressSize = Marshal.SizeOf<SocketAddress>();
        
        private SocketAddress _address;
        private Socket _socket;
        private IoHandle _io;
        private WorkHandle _accept;
        private AcceptEx _acceptFn;
        
        private Listener()
        {

        }

        public static Listener OnAddress(System.Net.IPEndPoint listenOn)
        {
            WinsockImports.Startup();

            SocketAddress address = SocketAddress.FromIPEndPoint(listenOn);
            Socket listenSocket = SocketFactory.Alloc();
            AcceptEx acceptFn = WinsockImports.Initalize(listenSocket);
            
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
            var bindResult = WinsockImports.bind(_socket, ref _address, _socketAddressSize);
            if (bindResult != WinsockImports.Success)
            {
                throw new Exception("bind failed");
            }
            
            var listenResult = WinsockImports.listen(_socket, 0);
            if (listenResult != WinsockImports.Success)
            {
                throw new Exception("listen failed");
            }

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

            Close();
        }
        
        private void OnAccept(CallbackInstance instance, IntPtr context, ConnectionOverlapped overlapped, uint result, uint transfered, IoHandle io)
        {
            if (result != WinsockImports.Success)
            {
                Close();
                throw new Exception($"accept failed: {result}");
            }

            _accept.Submit();
            
            overlapped.Connection.OnAccept(overlapped, transfered);
        }
        
        private void AcceptNextConnection(CallbackInstance callbackInstance, IntPtr context, WorkHandle work)
        {
            // TODO: Use a proper buffer
            var bufferSize = 4096;
            var requestBuffer = Buf.Alloc(bufferSize);
            var body = "Hello, world!";
            var responseBuffer = Buf.Alloc(Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}"));

            var acceptSocket = SocketFactory.Alloc();
            var connection = ConnectionOverlapped.ForNewConnection(acceptSocket, requestBuffer, responseBuffer);
            
            Accept(acceptSocket, requestBuffer, connection);
        }

        private void Accept(Socket acceptSocket, Buf requestBuffer, ConnectionOverlapped connectionHandle)
        {
            var addressSize = _socketAddressSize + 16;
            var readSize = requestBuffer.Length - (addressSize * 2);

            _io.Start();
            var success = _acceptFn(
                _socket, 
                acceptSocket, 
                requestBuffer.Pointer, 
                (uint)readSize, 
                (uint)addressSize, 
                (uint)addressSize, 
                out uint received, 
                connectionHandle);

            if (!success)
            {
                var error = WinsockImports.WSAGetLastError();

                if (error != WinsockImports.IOPending)
                {
                    Close();
                    throw new Exception($"accept failed: {error}");
                }
            }
        }

        private void Close()
        {
            var closeResult = WinsockImports.closesocket(_socket);

            _io.Cancel();

            if (closeResult != WinsockImports.Success)
            {
                throw new Exception($"Close failed: {closeResult}");
            }
        }
    }
}
