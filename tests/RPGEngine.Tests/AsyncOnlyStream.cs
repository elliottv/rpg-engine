namespace RPGEngine.Tests;

/// <summary>
/// A read-only wrapper stream that only supports asynchronous reads: the synchronous
/// <see cref="Read(byte[], int, int)"/>, <see cref="Position"/> and <see cref="Seek"/> members
/// throw, <see cref="CanSeek"/> is <see langword="false"/>, and <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>
/// delegates to the wrapped stream. This mirrors network/browser streams that only expose async
/// reads and proves that the async asset loaders never perform a blocking synchronous read of
/// the caller's stream.
/// </summary>
internal sealed class AsyncOnlyStream : Stream
{
    private readonly Stream _inner;

    public AsyncOnlyStream(Stream inner)
    {
        _inner = inner;
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException(
        "Length is not available on an async-only stream.");

    public override long Position
    {
        get => throw new NotSupportedException("Position is not supported on an async-only stream.");
        set => throw new NotSupportedException("Position is not supported on an async-only stream.");
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Synchronous reads are not supported on an async-only stream.");

    public override int Read(Span<byte> buffer)
        => throw new NotSupportedException("Synchronous reads are not supported on an async-only stream.");

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException("Seeking is not supported on an async-only stream.");

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
