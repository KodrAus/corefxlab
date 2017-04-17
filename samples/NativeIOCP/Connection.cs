using NativeIOCP.ThreadPool;
using NativeIOCP.Winsock;
using System;

namespace NativeIOCP
{
    class Connection
    {
        enum State
        {
            Reading = 0,
            Writing = 1
        }

        private Action _freeFn;
        private State _ioState;
        private Socket _socket;
        private IoHandle _io;
        private bool _closed;

        private Buf _requestBuffer;
        private Buf _responseBuffer;
        
        public Connection(Socket socket, Buf requestBuffer, Buf responseBuffer)
        {
            _requestBuffer = requestBuffer;
            _responseBuffer = responseBuffer;
            
            _socket = socket;

            _ioState = State.Reading;
            _io = IoHandle.Create(CallbackEnvironment.Default(), IntPtr.Zero, _socket, OnComplete);
        }

        public void SetFreeCallback(Action freeFn)
        {
            _freeFn = freeFn;
        }

        public void OnAccept(Overlapped overlapped, uint bytesTransfered)
        {
            if (bytesTransfered > 0)
            {
                OnReadComplete(overlapped, bytesTransfered);
            }
            else
            {
                DoRead(overlapped);
            }
        }

        public void Close()
        {
            if (!_closed)
            {
                _closed = true;

                var closeResult = WinsockImports.closesocket(_socket);

                Free();

                if (closeResult != WinsockImports.Success)
                {
                    throw new Exception($"Close failed: {closeResult}");
                }
            }
        }

        private void OnComplete(CallbackInstance instance, IntPtr context, Overlapped overlapped, uint ioResult, uint transfered, IoHandle io)
        {
            switch (_ioState)
            {
                case State.Reading:
                    OnReadComplete(overlapped, transfered);
                    break;
                case State.Writing:
                    OnWriteComplete(overlapped, transfered);
                    break;
            }
        }

        private void OnReadComplete(Overlapped overlapped, uint bytesTransfered)
        {
            if (bytesTransfered == 0)
            {
                Close();
            }
            else
            {
                DoWrite(overlapped);
            }
        }

        private void OnWriteComplete(Overlapped overlapped, uint bytesTransfered)
        {
            DoRead(overlapped);
        }

        private void DoRead(Overlapped overlapped)
        {
            _ioState = State.Reading;

            var wsabufs = WSABufs.Alloc(_requestBuffer);

            _io.Start();
            var readResult = WinsockImports.WSARecv(_socket, wsabufs, 1, out uint received, WinsockImports.ReadFlags.AddrOfPinnedObject(), overlapped, null);
            if (readResult != WinsockImports.Success)
            {
                var error = WinsockImports.WSAGetLastError();

                if (error != WinsockImports.IOPending)
                {
                    Free();
                    throw new Exception($"read failed: {readResult}");
                }
            }
        }

        private void DoWrite(Overlapped overlapped)
        {
            _ioState = State.Writing;
            
            var wsabufs = WSABufs.Alloc(_responseBuffer);

            _io.Start();
            var writeResult = WinsockImports.WSASend(_socket, wsabufs, 1, out uint sent, 0, overlapped, null);
            if (writeResult != WinsockImports.Success)
            {
                var error = WinsockImports.WSAGetLastError();

                if (error != WinsockImports.IOPending)
                {
                    Free();
                    throw new Exception($"write failed: {writeResult}");
                }
            }
        }
        
        private void Free()
        {
            _io.Cancel();
            _requestBuffer.Free();
            _responseBuffer.Free();

            _freeFn?.Invoke();
        }
    }
}
