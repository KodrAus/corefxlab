using NativeIOCP.Winsock;
using System;
using System.Net;

/*
    TODO:
    - Timing issue with socket close/accept and read? Issues on handles completing
    - Memory leaks: Stuff still isn't freed properly.
      The native thread pools require a lot of pointers with unknown lifetimes.
      That means getting handles to things and remembering to call Free when they're finished with.
      I've probably missed a bunch of places (like WSABufs, but am replacing that).
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
