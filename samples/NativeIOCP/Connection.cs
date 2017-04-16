using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;
using System.Runtime.InteropServices;

namespace NativeIOCP
{
    enum State
    {
        IsReading = 0,
        IsWriting = 1
    }

    public struct Connection
    {
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

        public void OnAccept(Overlapped overlapped)
        {
            _io = IoHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, _socket, OnReadOrWrite);
            Read(overlapped);
        }

        private void Read(Overlapped overlapped)
        {
            var buf = WSABufs.Alloc(_buf);

            _io.Start();
            var readResult = WinsockImports.WSARecv(_socket, buf, 1, out uint received, _flags.AddrOfPinnedObject(), overlapped, null);
            if (readResult != WinsockImports.Success && readResult != WinsockImports.IOPending)
            {
                _io.Cancel();
                throw new Exception($"read failed: {readResult}");
            }
        }

        private void OnReadOrWrite(
            CallbackInstance callbackInstance,
            IntPtr context,
            Overlapped overlapped,
            uint ioResult,
            uint bytesTransfered,
            IoHandle io)
        {
            Console.WriteLine($"Read: {bytesTransfered}");

            var str = _buf.AsString((int)bytesTransfered);

            Console.WriteLine(str);
        }
    }
}
