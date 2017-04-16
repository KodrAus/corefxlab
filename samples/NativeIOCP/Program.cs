using NativeIOCP.Winsock;
using System;
using System.Net;

/*
    TODO:
    - Memory leaks: NOTHING IS FREED AT THIS POINT.
      We can probably keep the GCHandles in a separate object that stays in managed code
      and cleans up after the connection when it's done.
      So the factory methods for sockets and buffers can return a SocketHandle { GCHandle handle, Socket Value() } or something.
      Having separate pins for everything kind of sucks though.
    - Use Pipelines. That'll replace the static buffer we're using right now.
    - Split out the imports and remove dead code
*/

namespace NativeIOCP
{
    public class Program
    {
        
        static void Main(string[] args)
        {
            var listener = Listener.OnAddress(new IPEndPoint(IPAddress.Loopback, 5000));

            listener.Listen();

            Console.WriteLine("Listening");

            listener.Wait();

            WinsockImports.WSACleanup();
        }
    }
}
