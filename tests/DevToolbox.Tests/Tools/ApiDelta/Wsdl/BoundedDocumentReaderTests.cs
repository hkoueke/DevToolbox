using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class BoundedDocumentReaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_oversized_file_is_refused_without_reading_any_content(bool asynchronous)
    {
        using ObservedStream stream = new(new byte[100], reportedLength: long.MaxValue);

        byte[]? result = await ReadDocument(stream, 16, asynchronous);

        result.Should().BeNull();
        stream.ReadCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_file_growing_after_the_size_check_is_stopped_at_the_budget_plus_one_byte(bool asynchronous)
    {
        using ObservedStream stream = new(new byte[100_000], reportedLength: 8);

        byte[]? result = await ReadDocument(stream, 16, asynchronous);

        result.Should().BeNull();
        stream.BytesRead.Should().Be(17);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 0)]
    [InlineData(false, 16, 16)]
    [InlineData(true, 16, 16)]
    [InlineData(false, 8, 16)]
    [InlineData(true, 8, 16)]
    [InlineData(false, 65_537, 65_537)]
    [InlineData(true, 65_537, 65_537)]
    public async Task Content_at_or_below_the_limit_is_preserved(bool asynchronous, int length, long limit)
    {
        byte[] expected = Enumerable.Repeat((byte)65, length).ToArray();
        using ObservedStream stream = new(expected, reportedLength: length);

        byte[]? result = await ReadDocument(stream, limit, asynchronous);

        result.Should().Equal(expected);
        stream.BytesRead.Should().Be(length);
    }

    [Fact]
    public async Task Cancellation_stops_the_async_reader_before_reading_content()
    {
        using ObservedStream stream = new(new byte[16], reportedLength: 16);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Func<Task> read = async () => _ = await BoundedDocumentReader.ReadAsync(stream, 16, cancellation.Token);

        await read.Should().ThrowAsync<OperationCanceledException>();
        stream.ReadCalls.Should().Be(0);
    }

    private static Task<byte[]?> ReadDocument(ObservedStream stream, long limit, bool asynchronous) => asynchronous
        ? BoundedDocumentReader.ReadAsync(stream, limit, CancellationToken.None)
        : Task.FromResult(BoundedDocumentReader.Read(stream, limit));

    // The reported length models the handle's metadata before a concurrent append.
    private sealed class ObservedStream(byte[] content, long reportedLength) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);
        internal int BytesRead { get; private set; }
        internal int ReadCalls { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => reportedLength;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            int count = _inner.Read(buffer);
            BytesRead += count;
            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
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
}
