// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace System.Buffers
{
    [DebuggerTypeProxy(typeof(BufferDebuggerView<>))]
    public struct Buffer<T> : IEquatable<Buffer<T>>, IEquatable<ReadOnlyBuffer<T>>
    {
        readonly OwnedBuffer<T> _owner;
        readonly int _index;
        readonly int _length;

        internal Buffer(OwnedBuffer<T> owner, int index, int length)
        {
            _owner = owner;
            _index = index;
            _length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyBuffer<T>(Buffer<T> buffer)
        {
            return new ReadOnlyBuffer<T>(buffer._owner, buffer._index, buffer._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Buffer<T>(T[] array)
        {
            var owner = new Internal.OwnedArray<T>(array);
            return owner.Buffer;
        }

        public static Buffer<T> Empty { get; } = Internal.OwnerEmptyMemory<T>.Shared.Buffer;

        public int Length => _length;

        public bool IsEmpty => Length == 0;

        public IMemoryMetadata Metadata => _owner.Metadata;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer<T> Slice(int index)
        {
            return new Buffer<T>(_owner, _index + index, _length - index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer<T> Slice(int index, int length)
        {
            return new Buffer<T>(_owner, _index + index, length);
        }

        public Span<T> Span {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _owner.GetSpanInternal(_index, _length); }
        }

        public DisposableReservation<T> Reserve() => new DisposableReservation<T>(_owner);

        public unsafe BufferHandle Pin() => BufferHandle.Create(_owner, _index);

        public unsafe bool TryGetPointer(out void* pointer)
        {
            if (!_owner.TryGetPointerInternal(out pointer)) {
                return false;
            }
            pointer = Add(pointer, _index);
            return true;
        }

        public bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (!_owner.TryGetArrayInternal(out buffer)) {
                return false;
            }
            buffer = new ArraySegment<T>(buffer.Array, buffer.Offset + _index, _length);
            return true;
        }

        internal static unsafe void* Add(void* pointer, int offset)
        {
            return (byte*)pointer + ((ulong)Unsafe.SizeOf<T>() * (ulong)offset);
        }

        public T[] ToArray() => Span.ToArray();

        public void CopyTo(Span<T> span) => Span.CopyTo(span);

        public void CopyTo(Buffer<T> buffer) => Span.CopyTo(buffer.Span);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if(!(obj is Buffer<T>)) {
                return false;
            }

            var other = (Buffer<T>)obj;
            return Equals(other);
        }
        public bool Equals(Buffer<T> other)
        {
            return
                _owner == other._owner &&
                _index == other._index &&
                _length == other._length;
        }
        public bool Equals(ReadOnlyBuffer<T> other)
        {
            return other.Equals(this);
        }
        public static bool operator==(Buffer<T> left, Buffer<T> right)
        {
            return left.Equals(right);
        }
        public static bool operator!=(Buffer<T> left, Buffer<T> right)
        {
            return !left.Equals(right);
        }
        public static bool operator ==(Buffer<T> left, ReadOnlyBuffer<T> right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(Buffer<T> left, ReadOnlyBuffer<T> right)
        {
            return !left.Equals(right);
        }

        [EditorBrowsable( EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return HashingHelper.CombineHashCodes(_owner.GetHashCode(), _index.GetHashCode(), _length.GetHashCode());
        }
    }
}
