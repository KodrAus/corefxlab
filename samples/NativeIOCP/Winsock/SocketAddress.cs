using System.Runtime.InteropServices;

namespace NativeIOCP.Winsock
{
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct SocketAddress
    {
        public AddressFamilies Family;
        public ushort Port;
        public Ipv4InternetAddress IpAddress;
        public fixed byte Padding[8];

        public static SocketAddress FromIPEndPoint(System.Net.IPEndPoint ipEndPoint)
        {
            var listenOnBytes = ipEndPoint.Address.MapToIPv4().GetAddressBytes();

            ushort port = (ushort)ipEndPoint.Port;

            var inAddress = new Ipv4InternetAddress();
            inAddress.Byte1 = listenOnBytes[0];
            inAddress.Byte2 = listenOnBytes[1];
            inAddress.Byte3 = listenOnBytes[2];
            inAddress.Byte4 = listenOnBytes[3];

            var sa = new SocketAddress();
            sa.Family = AddressFamilies.Internet;
            sa.Port = WinsockImports.htons(port);
            sa.IpAddress = inAddress;

            return sa;
        }
    }

    enum AddressFamilies : short
    {
        Internet = 2,
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    struct Ipv4InternetAddress
    {
        [FieldOffset(0)]
        public byte Byte1;
        [FieldOffset(1)]
        public byte Byte2;
        [FieldOffset(2)]
        public byte Byte3;
        [FieldOffset(3)]
        public byte Byte4;
    }
}
