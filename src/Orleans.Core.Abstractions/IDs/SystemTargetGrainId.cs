using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Orleans.Runtime
{
    /// <summary>
    /// Identifies a system target.
    /// </summary>
    [Immutable]
    public readonly struct SystemTargetGrainId : IEquatable<SystemTargetGrainId>, IComparable<SystemTargetGrainId>
    {
        private const char SegmentSeparator = '+';

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemTargetGrainId"/> struct.
        /// </summary>
        /// <param name="grainId">
        /// The grain id.
        /// </param>
        private SystemTargetGrainId(GrainId grainId)
        {
            this.GrainId = grainId;
        }

        /// <summary>
        /// Gets the underlying identity.
        /// </summary>
        public GrainId GrainId { get; }

        /// <summary>
        /// Creates a new <see cref="SystemTargetGrainId"/> instance.
        /// </summary>
        /// <param name="kind">
        /// The grain type.
        /// </param>
        /// <param name="address">
        /// The server which the system target exists on.
        /// </param>
        /// <returns>
        /// A <see cref="SystemTargetGrainId"/>.
        /// </returns>
        public static SystemTargetGrainId Create(GrainType kind, SiloAddress address) => new SystemTargetGrainId(new GrainId(kind, new IdSpan(address.ToUtf8String())));

        /// <summary>
        /// Creates a new <see cref="SystemTargetGrainId"/> instance.
        /// </summary>
        /// <param name="kind">
        /// The grain type.
        /// </param>
        /// <param name="address">
        /// The server which the system target exists on.
        /// </param>
        /// <param name="extraIdentifier">
        /// An optional key extension.
        /// </param>
        /// <returns>
        /// A <see cref="SystemTargetGrainId"/>.
        /// </returns>
        public static SystemTargetGrainId Create(GrainType kind, SiloAddress address, string extraIdentifier)
        {
            var addr = address.ToUtf8String();
            if (extraIdentifier is string)
            {
                var extraLen = Encoding.UTF8.GetByteCount(extraIdentifier);
                var buf = new byte[addr.Length + 1 + extraLen];
                addr.CopyTo(buf.AsSpan());
                buf[addr.Length] = (byte)SegmentSeparator;
                Encoding.UTF8.GetBytes(extraIdentifier, 0, extraIdentifier.Length, buf, addr.Length + 1);
                addr = buf;
            }

            return new SystemTargetGrainId(new GrainId(kind, new IdSpan(addr)));
        }

        /// <summary>
        /// Returns <see langword="true"/> if the provided instance represents a system target, <see langword="false"/> if otherwise.
        /// </summary>
        /// <param name="id">
        /// The grain id.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the value is a system target grain id, <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsSystemTargetGrainId(in GrainId id) => id.Type.AsSpan().StartsWith(GrainTypePrefix.SystemTargetPrefixBytes.Span);

        /// <summary>
        /// Converts the provided <see cref="GrainId"/> to a <see cref="SystemTargetGrainId"/>. A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="grainId">
        /// The grain id.
        /// </param>
        /// <param name="systemTargetId">
        /// The resulting system target id.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the value is a system target grain id, <see langword="false"/> otherwise.
        /// </returns>
        public static bool TryParse(GrainId grainId, out SystemTargetGrainId systemTargetId)
        {
            if (!IsSystemTargetGrainId(grainId))
            {
                systemTargetId = default;
                return false;
            }

            systemTargetId = new SystemTargetGrainId(grainId);
            return true;
        }

        /// <summary>
        /// Returns a new <see cref="SystemTargetGrainId"/> targeting the provided address.
        /// </summary>
        /// <param name="siloAddress">
        /// The silo address.
        /// </param>
        /// <returns>
        /// A new <see cref="SystemTargetGrainId"/> targeting the provided address.
        /// </returns>
        public SystemTargetGrainId WithSiloAddress(SiloAddress siloAddress)
        {
            var addr = siloAddress.ToUtf8String();
            var key = this.GrainId.Key.AsSpan();
            if (key.IndexOf((byte)SegmentSeparator) is int index && index >= 0)
            {
                var extraIdentifier = key.Slice(index + 1);

                var buf = new byte[addr.Length + 1 + extraIdentifier.Length];
                addr.CopyTo(buf.AsSpan());
                buf[addr.Length] = (byte)SegmentSeparator;
                extraIdentifier.CopyTo(buf.AsSpan(addr.Length + 1));
                addr = buf;
            }

            return new SystemTargetGrainId(new GrainId(GrainId.Type, new IdSpan(addr)));
        }

        /// <summary>
        /// Gets the <see cref="SiloAddress"/> of the system target.
        /// </summary>
        /// <returns>
        /// The silo address corresponding to this system target id.
        /// </returns>
        public SiloAddress GetSiloAddress()
        {
            var key = this.GrainId.Key.AsSpan();
            if (key.IndexOf((byte)SegmentSeparator) is int index && index >= 0)
            {
                key = key.Slice(0, index);
            }

            return SiloAddress.FromUtf8String(key);
        }

        /// <summary>
        /// Creates a <see cref="GrainId"/> for a grain service.
        /// </summary>
        /// <param name="typeCode">
        /// The type code.
        /// </param>
        /// <param name="grainSystemId">
        /// The system id.
        /// </param>
        /// <param name="address">
        /// The silo address.
        /// </param>
        /// <returns>A grain id for a grain service instance.</returns>
        public static GrainId CreateGrainServiceGrainId(int typeCode, string grainSystemId, SiloAddress address)
            => CreateGrainServiceGrainId(CreateGrainServiceGrainType(typeCode, grainSystemId), address);

        /// <summary>
        /// Creates a <see cref="GrainId"/> for a grain service.
        /// </summary>
        /// <param name="typeCode">
        /// The type code.
        /// </param>
        /// <param name="grainSystemId">
        /// The system id.
        /// </param>
        /// <returns>A grain id for a grain service instance.</returns>
        internal static GrainType CreateGrainServiceGrainType(int typeCode, string grainSystemId)
        {
            var extraLen = grainSystemId is null ? 0 : Encoding.UTF8.GetByteCount(grainSystemId);
            var buf = new byte[GrainTypePrefix.GrainServicePrefix.Length + 8 + extraLen];
            GrainTypePrefix.GrainServicePrefixBytes.Span.CopyTo(buf);
            Utf8Formatter.TryFormat(typeCode, buf.AsSpan(GrainTypePrefix.GrainServicePrefix.Length), out var len, new StandardFormat('X', 8));
            Debug.Assert(len == 8);
            if (grainSystemId != null) Encoding.UTF8.GetBytes(grainSystemId, 0, grainSystemId.Length, buf, buf.Length - extraLen);
            return new GrainType(buf);
        }

        /// <summary>
        /// Creates a <see cref="GrainId"/> for a grain service.
        /// </summary>
        /// <param name="grainType">
        /// The grain type.
        /// </param>
        /// <param name="address">
        /// The silo address.
        /// </param>
        /// <returns>A grain id for a grain service instance.</returns>
        internal static GrainId CreateGrainServiceGrainId(GrainType grainType, SiloAddress address)
            => new GrainId(grainType, new IdSpan(address.ToUtf8String()));

        /// <summary>
        /// Creates a system target <see cref="GrainType"/> with the provided name.
        /// </summary>
        /// <param name="name">
        /// The system target grain type name.
        /// </param>
        /// <returns>
        /// The grain type.
        /// </returns>
        public static GrainType CreateGrainType(string name) => GrainType.Create($"{GrainTypePrefix.SystemTargetPrefix}{name}");

        /// <inheritdoc/>
        public bool Equals(SystemTargetGrainId other) => this.GrainId.Equals(other.GrainId);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SystemTargetGrainId observer && this.Equals(observer);

        /// <inheritdoc/>
        public override int GetHashCode() => this.GrainId.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => this.GrainId.ToString();

        /// <inheritdoc/>
        public int CompareTo(SystemTargetGrainId other) => this.GrainId.CompareTo(other.GrainId);

        /// <summary>
        /// Compares the provided operands for equality.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the provided values are equal, otherwise <see langword="false"/>.</returns>
        public static bool operator ==(SystemTargetGrainId left, SystemTargetGrainId right) => left.Equals(right);

        /// <summary>
        /// Compares the provided operands for inequality.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the provided values are not equal, otherwise <see langword="false"/>.</returns>
        public static bool operator !=(SystemTargetGrainId left, SystemTargetGrainId right) => !(left == right);

        /// <summary>
        /// Compares the provided operands and returns <see langword="true"/> if the left operand is less than the right operand, otherwise <see langword="false"/>.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the left operand is less than the right operand, otherwise <see langword="false"/>.</returns>
        public static bool operator <(SystemTargetGrainId left, SystemTargetGrainId right) => left.CompareTo(right) < 0;

        /// <summary>
        /// Compares the provided operands and returns <see langword="true"/> if the left operand is less than or equal to the right operand, otherwise <see langword="false"/>.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the left operand is less than or equal to the right operand, otherwise <see langword="false"/>.</returns>
        public static bool operator <=(SystemTargetGrainId left, SystemTargetGrainId right) => left.CompareTo(right) <= 0;

        /// <summary>
        /// Compares the provided operands and returns <see langword="true"/> if the left operand is greater than the right operand, otherwise <see langword="false"/>.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the left operand is greater than the right operand, otherwise <see langword="false"/>.</returns>
        public static bool operator >(SystemTargetGrainId left, SystemTargetGrainId right) => left.CompareTo(right) > 0;

        /// <summary>
        /// Compares the provided operands and returns <see langword="true"/> if the left operand is greater than or equal to the right operand, otherwise <see langword="false"/>.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns><see langword="true"/> if the left operand is greater than or equal to the right operand, otherwise <see langword="false"/>.</returns>
        public static bool operator >=(SystemTargetGrainId left, SystemTargetGrainId right) => left.CompareTo(right) >= 0;

        /// <summary>
        /// An <see cref="IEqualityComparer{T}"/> and <see cref="IComparer{T}"/> implementation for <see cref="SystemTargetGrainId"/>.
        /// </summary>
        public sealed class Comparer : IEqualityComparer<SystemTargetGrainId>, IComparer<SystemTargetGrainId>
        {
            /// <summary>
            /// Gets the singleton <see cref="Comparer"/> instance.
            /// </summary>
            public static Comparer Instance { get; } = new Comparer();

            /// <inheritdoc/>
            public int Compare(SystemTargetGrainId x, SystemTargetGrainId y) => x.CompareTo(y);

            /// <inheritdoc/>
            public bool Equals(SystemTargetGrainId x, SystemTargetGrainId y) => x.Equals(y);

            /// <inheritdoc/>
            public int GetHashCode(SystemTargetGrainId obj) => obj.GetHashCode();
        }
    }
}
