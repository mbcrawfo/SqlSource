using System;
using System.Security.Cryptography;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The key of one renaming run: 32 random bytes, made for the run and never written, so that a replacement cannot be
/// guessed back to its name by hashing a dictionary.
/// </summary>
/// <remarks>It holds its bytes privately and prints none, so that no log, message or dump can carry them.</remarks>
internal sealed class RenamingKey
{
    /// <summary>The number of bytes of a key.</summary>
    public const int Length = 32;

    private readonly byte[] _bytes;

    private RenamingKey(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>A key of random bytes.</summary>
    public static RenamingKey Random() => new(RandomNumberGenerator.GetBytes(Length));

    /// <summary>A key of given bytes, for a test that compares runs.</summary>
    /// <exception cref="ArgumentException">The bytes are not 32.</exception>
    public static RenamingKey Of(ReadOnlySpan<byte> bytes) =>
        bytes.Length == Length
            ? new RenamingKey(bytes.ToArray())
            : throw new ArgumentException($"A key is {Length} bytes, not {bytes.Length}.", nameof(bytes));

    /// <summary>The HMAC-SHA256 of the data under this key.</summary>
    public byte[] Hash(byte[] data) => HMACSHA256.HashData(_bytes, data);

    /// <inheritdoc />
    public override string ToString() => nameof(RenamingKey);
}
