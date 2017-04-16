using NativeIOCP.Winsock;
using System;
using System.Net;

/*
    TODO:
    - Timing issue with socket close/accept and read? Issues on handles completing
    - Memory leaks: NOTHING IS FREED AT THIS POINT.
      We can probably keep the GCHandles in a separate object that stays in managed code
      and cleans up after the connection when it's done.
      So the factory methods for sockets and buffers can return a SocketHandle { GCHandle handle, Socket Value() } or something.
      Having separate pins for everything kind of sucks though.
    - Use Pipelines. That'll replace the static buffer we're using right now.
    - API leaks
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

            Console.ReadLine();

            WinsockImports.WSACleanup();
        }
    }
}
