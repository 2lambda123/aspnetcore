// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Connections.Helpers
{
    public sealed class DuplexPipeStream : Stream
    {
        private readonly Stream _readStream;
        private readonly Stream _writeStream;

        public DuplexPipeStream(IDuplexPipe duplexPipe)
        {
            _readStream = duplexPipe.Input.AsStream(leaveOpen: true);
            _writeStream = duplexPipe.Output.AsStream(leaveOpen: true);
        }

        public override bool CanSeek => false;

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _readStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return _readStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _readStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _readStream.EndRead(asyncResult);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _readStream.Read(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _writeStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _writeStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _writeStream.EndWrite(asyncResult);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writeStream.Write(buffer, offset, count);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _writeStream.FlushAsync(cancellationToken);
        }

        public override void Flush()
        {
            _writeStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

#if NETSTANDARD2_1 || NETCOREAPP
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _readStream.ReadAsync(buffer, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _writeStream.WriteAsync(buffer);
        }
#endif

#if NETCOREAPP
        public override Task CopyToAsync(Func<ReadOnlyMemory<byte>, object, CancellationToken, ValueTask> callback, object state, int bufferSize, CancellationToken cancellationToken)
        {
            return _readStream.CopyToAsync(callback, state, bufferSize, cancellationToken);
        }
#endif
    }
}
