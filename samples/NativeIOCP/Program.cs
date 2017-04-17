using NativeIOCP.Winsock;
using System;
using System.Net;

/*
    TODO:
    - Handle errors in callbacks vs operations better.
      Is it possible for an error to get reported to the callback rather than the op?
    - Memory leaks: Stuff still isn't freed properly.
      The native thread pools require a lot of pointers with unknown lifetimes.
      That means getting handles to things and remembering to call Free when they're finished with.
      I've probably missed a bunch of places (like WSABufs, but am replacing that).
    - Use Pipelines. That'll replace the static buffer we're using right now.
    - API leaks. Don't really like the ConnectionOverlapped struct that's passed around.
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
