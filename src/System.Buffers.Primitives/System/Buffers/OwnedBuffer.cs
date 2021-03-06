// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Buffers
{
    public abstract class OwnedBuffer<T> : IDisposable, IRetainable
    {
        protected OwnedBuffer() { }

        public abstract int Length { get; }

        public abstract Span<T> AsSpan(int index, int length);

        public virtual Span<T> AsSpan() => AsSpan(0, Length);

        public Buffer<T> Buffer => new Buffer<T>(this, 0, Length);

        public ReadOnlyBuffer<T> ReadOnlyBuffer => new ReadOnlyBuffer<T>(this, 0, Length);

        public abstract BufferHandle Pin(int index = 0);

        internal protected abstract bool TryGetArray(out ArraySegment<T> buffer);

        #region Lifetime Management
        public abstract bool IsDisposed { get; }

        public void Dispose()
        {
            if (IsRetained) throw new InvalidOperationException("outstanding references detected.");
            Dispose(true);
        }

        protected abstract void Dispose(bool disposing);

        public abstract bool IsRetained { get; }

        public abstract void Retain();

        public abstract void Release();
        #endregion

        protected static unsafe void* Add(void* pointer, int offset)
        {
            return (byte*)pointer + ((ulong)Unsafe.SizeOf<T>() * (ulong)offset);
        }
    }
}