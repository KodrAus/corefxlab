using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NativeIOCP
{
    class Connection
    {
        enum State
        {
            Reading = 0,
            Writing = 1
        }

        private Listener _listener;
        private State _ioState;
        private Socket _socket;
        private Buf _buf;
        private IoHandle _io;
        private GCHandle _flags;
        
        public Connection(Listener listener, Socket socket, Buf buf)
        {
            _ioState = State.Reading;
            _socket = socket;
            _buf = buf;
            _io = new IoHandle();
            _flags = GCHandle.Alloc(0, GCHandleType.Pinned);
            _listener = listener;
        }

        public void OnAccept(Overlapped overlapped, uint bytesTransfered)
        {
            _io = IoHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, _socket, OnComplete);

            if (bytesTransfered > 0)
            {
                OnRead(overlapped, bytesTransfered);
            }
            else
            {
                DoRead(overlapped);
            }
        }

        private void OnComplete(
            CallbackInstance callbackInstance,
            IntPtr context,
            Overlapped overlapped,
            uint ioResult,
            uint bytesTransfered,
            IoHandle io)
        {
            switch (_ioState)
            {
                case State.Reading:
                    OnRead(overlapped, bytesTransfered);
                    break;
                case State.Writing:
                    OnWrite(overlapped, bytesTransfered);
                    break;
            }
        }

        private void OnRead(Overlapped overlapped, uint bytesTransfered)
        {
            if (bytesTransfered == 0)
            {
                Close();
            }
            else
            {
                var str = _buf.AsString((int)bytesTransfered);

                Console.WriteLine($"Read: {bytesTransfered}");
                Console.WriteLine(str);
                
                DoWrite(overlapped);
            }
        }

        private void OnWrite(Overlapped overlapped, uint bytesTransfered)
        {
            Console.WriteLine($"Written: {bytesTransfered}");

            Close();
        }

        private void DoRead(Overlapped overlapped)
        {
            _ioState = State.Reading;

            var wsabufs = WSABufs.Alloc(_buf);

            // TODO: Read may complete synchronously here.
            // I think there's a sockopt to prevent callbacks when results are synchronous.
            _io.Start();
            var readResult = WinsockImports.WSARecv(_socket, wsabufs, 1, out uint received, _flags.AddrOfPinnedObject(), overlapped, null);
            if (readResult != WinsockImports.Success && readResult != WinsockImports.IOPending)
            {
                _io.Cancel();
                throw new Exception($"read failed: {readResult}");
            }
        }

        private void DoWrite(Overlapped overlapped)
        {
            _ioState = State.Writing;

            var body = "Hello, world!";
            var res = $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}";
            var buf = Buf.Alloc(Encoding.UTF8.GetBytes(res));

            Console.WriteLine(res);

            var wsabufs = WSABufs.Alloc(buf);

            _io.Start();
            var writeResult = WinsockImports.WSASend(_socket, wsabufs, 1, out uint sent, 0, overlapped, null);
            if (writeResult != WinsockImports.Success && writeResult != WinsockImports.IOPending)
            {
                _io.Cancel();
                throw new Exception($"write failed: {writeResult}");
            }
        }

        private void Close()
        {
            _io.Cancel();
            WinsockImports.closesocket(_socket);
            
            _listener.Free(_socket);
        }
    }
}
