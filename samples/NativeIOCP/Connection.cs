using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;
using System.Runtime.InteropServices;

namespace NativeIOCP
{
    public struct Connection
    {
        enum State
        {
            IsReading = 0,
            IsWriting = 1
        }

        private Socket _socket;
        private Buf _buf;
        private IoHandle _io;
        private GCHandle _flags;
        
        public Connection(Socket socket, Buf buf)
        {
            _socket = socket;
            _buf = buf;
            _io = new IoHandle();
            _flags = GCHandle.Alloc(0, GCHandleType.Pinned);
        }

        public void OnAccept(Overlapped overlapped, uint bytesTransfered)
        {
            _io = IoHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, _socket, OnComplete);

            if (bytesTransfered > 0)
            {
                OnRead(bytesTransfered);
            }
            else
            {
                DoRead(overlapped);
            }
        }

        private void DoRead(Overlapped overlapped)
        {
            var buf = WSABufs.Alloc(_buf);

            // TODO: Read may complete synchronously here.
            // I think there's a sockopt to prevent callbacks when results are synchronous.
            _io.Start();
            var readResult = WinsockImports.WSARecv(_socket, buf, 1, out uint received, _flags.AddrOfPinnedObject(), overlapped, null);
            if (readResult != WinsockImports.Success && readResult != WinsockImports.IOPending)
            {
                _io.Cancel();
                throw new Exception($"read failed: {readResult}");
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
            OnRead(bytesTransfered);
        }

        private void OnRead(uint bytesTransfered)
        {
            Console.WriteLine($"Read: {bytesTransfered}");

            var str = _buf.AsString((int)bytesTransfered);

            Console.WriteLine(str);
        }
    }
}
