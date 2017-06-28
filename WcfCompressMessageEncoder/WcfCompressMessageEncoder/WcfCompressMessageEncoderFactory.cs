using System;
using System.IO;
using System.IO.Compression;
using System.ServiceModel.Channels;
using Brotli;

namespace WcfCompressMessageEncoder
{
    //This class is used to create the custom encoder (WcfCompressMessageEncoder)
    internal class WcfCompressMessageEncoderFactory : MessageEncoderFactory
    {
        private readonly MessageEncoder encoder;

        //The WcfCompress encoder wraps an inner encoder
        //We require a factory to be passed in that will create this inner encoder
        public WcfCompressMessageEncoderFactory(MessageEncoderFactory messageEncoderFactory, string compressionFormat)
        {
            if (messageEncoderFactory == null)
                throw new ArgumentNullException("messageEncoderFactory", "A valid message encoder factory must be passed to the WcfCompressEncoder");
            encoder = new WcfCompressMessageEncoder(messageEncoderFactory.Encoder, compressionFormat);
        }

        //The service framework uses this property to obtain an encoder from this encoder factory
        public override MessageEncoder Encoder => encoder;

        public override MessageVersion MessageVersion => encoder.MessageVersion;

        //This is the actual WcfCompress encoder
        private class WcfCompressMessageEncoder : MessageEncoder
        {
            private static readonly string WcfCompressContentType = "application/x-WcfCompress";

            //This implementation wraps an inner encoder that actually converts a WCF Message
            //into textual XML, binary XML or some other format. This implementation then compresses the results.
            //The opposite happens when reading messages.
            //This member stores this inner encoder.
            private readonly MessageEncoder innerEncoder;

            private readonly string compressionFormat;

            //We require an inner encoder to be supplied (see comment above)
            internal WcfCompressMessageEncoder(MessageEncoder messageEncoder, string compressionFormat)
            {
                if (messageEncoder == null)
                    throw new ArgumentNullException("messageEncoder", "A valid message encoder must be passed to the WcfCompressEncoder");
                innerEncoder = messageEncoder;
                this.compressionFormat = compressionFormat;
            }

            public override string ContentType => string.IsNullOrEmpty(compressionFormat) ? WcfCompressContentType : WcfCompressContentType + "-" + compressionFormat.ToLower();

            public override string MediaType => ContentType;

            //SOAP version to use - we delegate to the inner encoder for this
            public override MessageVersion MessageVersion => innerEncoder.MessageVersion;

            private Stream GetCompressStream(Stream inputStream, CompressionMode compressionMode)
            {
                switch (compressionFormat.ToLower())
                {
                    case "":
                        return null;
                    case "gzip":
                        return new GZipStream(inputStream, compressionMode, true);
                    case "deflate":
                        return new DeflateStream(inputStream, compressionMode, true);
                    case "brotli":
                        return new BrotliStream(inputStream, compressionMode, true);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(compressionFormat), $"Unknown compressionFormat value '{compressionFormat}'");
                }
            }

            //Helper method to compress an array of bytes
            private ArraySegment<byte> CompressBuffer(ArraySegment<byte> buffer, BufferManager bufferManager, int messageOffset)
            {
                var memoryStream = new MemoryStream();
                var compressStream = GetCompressStream(memoryStream, CompressionMode.Compress);
                if (compressStream == null) return buffer;

                using (var gzStream = compressStream)
                {
                    gzStream.Write(buffer.Array, buffer.Offset, buffer.Count);
                }

                byte[] compressedBytes = memoryStream.ToArray();
                var totalLength = messageOffset + compressedBytes.Length;
                byte[] bufferedBytes = bufferManager.TakeBuffer(totalLength);

                Array.Copy(compressedBytes, 0, bufferedBytes, messageOffset, compressedBytes.Length);

                bufferManager.ReturnBuffer(buffer.Array);
                //pbalas repair length of output array
                //ArraySegment<byte> byteArray = new ArraySegment<byte>(bufferedBytes, messageOffset, bufferedBytes.Length - messageOffset);
                ArraySegment<byte> byteArray = new ArraySegment<byte>(bufferedBytes, messageOffset, totalLength - messageOffset);

                return byteArray;
            }

            //Helper method to decompress an array of bytes
            private ArraySegment<byte> DecompressBuffer(ArraySegment<byte> buffer, BufferManager bufferManager)
            {
                var memoryStream = new MemoryStream(buffer.Array, buffer.Offset, buffer.Count);
                var decompressedStream = new MemoryStream();
                var decompressStream = GetCompressStream(memoryStream, CompressionMode.Decompress);
                if (decompressStream == null) return buffer;

                var totalRead = 0;
                var blockSize = 1024;
                byte[] tempBuffer = bufferManager.TakeBuffer(blockSize);
                using (var gzStream = decompressStream)
                {
                    while (true)
                    {
                        var bytesRead = gzStream.Read(tempBuffer, 0, blockSize);
                        if (bytesRead == 0)
                            break;
                        decompressedStream.Write(tempBuffer, 0, bytesRead);
                        totalRead += bytesRead;
                    }
                }
                bufferManager.ReturnBuffer(tempBuffer);

                byte[] decompressedBytes = decompressedStream.ToArray();
                byte[] bufferManagerBuffer = bufferManager.TakeBuffer(decompressedBytes.Length + buffer.Offset);
                Array.Copy(buffer.Array, 0, bufferManagerBuffer, 0, buffer.Offset);
                Array.Copy(decompressedBytes, 0, bufferManagerBuffer, buffer.Offset, decompressedBytes.Length);

                ArraySegment<byte> byteArray = new ArraySegment<byte>(bufferManagerBuffer, buffer.Offset, decompressedBytes.Length);
                bufferManager.ReturnBuffer(buffer.Array);

                return byteArray;
            }

            //One of the two main entry points into the encoder. Called by WCF to decode a buffered byte array into a Message.
            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                //Decompress the buffer
                ArraySegment<byte> decompressedBuffer = DecompressBuffer(buffer, bufferManager);
                //Use the inner encoder to decode the decompressed buffer
                var returnMessage = innerEncoder.ReadMessage(decompressedBuffer, bufferManager);
                returnMessage.Properties.Encoder = this;
                return returnMessage;
            }

            //One of the two main entry points into the encoder. Called by WCF to encode a Message into a buffered byte array.
            public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
            {
                //Use the inner encoder to encode a Message into a buffered byte array
                ArraySegment<byte> buffer = innerEncoder.WriteMessage(message, maxMessageSize, bufferManager, 0);
                //Compress the resulting byte array
                return CompressBuffer(buffer, bufferManager, messageOffset);
            }

            public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
            {
                //Pass false for the "leaveOpen" parameter to the GZipStream constructor.
                //This will ensure that the inner stream gets closed when the message gets closed, which
                //will ensure that resources are available for reuse/release.
                var gzStream = new GZipStream(stream, CompressionMode.Decompress, false);
                return innerEncoder.ReadMessage(gzStream, maxSizeOfHeaders);
            }

            public override void WriteMessage(Message message, Stream stream)
            {
                using (var gzStream = new GZipStream(stream, CompressionMode.Compress, true))
                {
                    innerEncoder.WriteMessage(message, gzStream);
                }

                // innerEncoder.WriteMessage(message, gzStream) depends on that it can flush data by flushing 
                // the stream passed in, but the implementation of GZipStream.Flush will not flush underlying
                // stream, so we need to flush here.
                stream.Flush();
            }
        }
    }
}