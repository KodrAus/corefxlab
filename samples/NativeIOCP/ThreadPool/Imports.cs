using NativeIOCP.Winsock;
using System;
using System.Runtime.InteropServices;

namespace NativeIOCP.ThreadPool
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    unsafe delegate void IoCallback(
        [In, Out] CallbackInstance callbackInstance,
        [In, Out, Optional] IntPtr context,
        [In, Out, Optional] Overlapped overlapped,
        [In] uint ioResult,
        [In] uint bytesTransfered,
        [In, Out] IoHandle io
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    delegate void WorkCallback(
        [In, Out] CallbackInstance callbackInstance,
        [In, Out, Optional] IntPtr context,
        [In, Out] WorkHandle work
    );

    [StructLayout(LayoutKind.Sequential)]
    struct WorkHandle
    {
        private IntPtr _value;

        public static WorkHandle Create(CallbackEnvironment environment, IntPtr context, WorkCallback callback)
        {
            return ThreadPoolImports.CreateThreadpoolWork(callback, context, environment);
        }

        public void Submit()
        {
            ThreadPoolImports.SubmitThreadpoolWork(this);
        }

        public void Wait(bool cancelPendingCallbacks)
        {
            ThreadPoolImports.WaitForThreadpoolWorkCallbacks(this, cancelPendingCallbacks);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IoHandle
    {
        private IntPtr _value;

        public static IoHandle Create(CallbackEnvironment environment, IntPtr context, Socket socket, IoCallback callback)
        {
            return ThreadPoolImports.CreateThreadpoolIo(socket, callback, context, environment);
        }
        
        public void Start()
        {
            ThreadPoolImports.StartThreadpoolIo(this);
        }

        public void Cancel()
        {
            ThreadPoolImports.CancelThreadpoolIo(this);
        }

        public void Wait(bool cancelPendingCallbacks)
        {
            ThreadPoolImports.WaitForThreadpoolIoCallbacks(this, cancelPendingCallbacks);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CallbackEnvironment
    {
        private IntPtr _value;

        public static CallbackEnvironment Default()
        {
            return new CallbackEnvironment { _value = IntPtr.Zero };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CallbackInstance
    {
        private IntPtr _value;
    }

    static class ThreadPoolImports
    {
        const string K32 = "Kernel32.dll";

        [DllImport(K32, SetLastError = true)]
        public static extern IoHandle CreateThreadpoolIo(
            [In] Socket handle, 
            [In] IoCallback callback, 
            [In, Out, Optional] IntPtr context,
            [In] CallbackEnvironment environment
        );

        [DllImport(K32, SetLastError = true)]
        public static extern void StartThreadpoolIo(
            [In, Out] IoHandle io
        );

        [DllImport(K32, SetLastError = true)]
        public static extern void CancelThreadpoolIo(
            [In, Out] IoHandle io
        );

        [DllImport(K32, SetLastError = true)]
        public static extern void WaitForThreadpoolIoCallbacks(
            [In, Out] IoHandle io,
            [In] bool cancelPendingCallbacks
        );

        [DllImport(K32, SetLastError = true)]
        public static extern WorkHandle CreateThreadpoolWork(
            [In] WorkCallback callback,
            [In, Out, Optional] IntPtr context,
            [In] CallbackEnvironment environment
        );

        [DllImport(K32, SetLastError = true)]
        public static extern void SubmitThreadpoolWork(
            [In, Out] WorkHandle work
        );

        [DllImport(K32, SetLastError = true)]
        public static extern void WaitForThreadpoolWorkCallbacks(
            [In, Out] WorkHandle work,
            [In] bool cancelPendingCallbacks
        );
    }
}
