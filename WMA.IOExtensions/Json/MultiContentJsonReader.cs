using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace WMA.IOExtensions.Json
{
    public class MultiContentJsonReader
    {
        /// <summary>
        /// Use this method to deserialize objects from stream and receive in the await foreach(..) loop
        /// </summary>
        /// <param name="stream">Stream to be read from</param>
        /// <param name="options">Options for PipeReder and JsonSerializer</param>
        /// <param name="deserializer">Inject custom deserialized from ReadOnlySequence to TValue</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="TValue">The type will be passed to JsonSerializer.Deserialize method</typeparam>
        /// <returns>IAsyncEnumerable of TValue to be iterated in await foreach(..) loop</returns>
        public static async IAsyncEnumerable<TValue?> DeserializeAsyncEnumerable<TValue>(
            Stream stream,
            MultiContentJsonReaderOptions? options = null,
            Func<ReadOnlySequence<byte>, TValue>? deserializer = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            var pipeReader = PipeReader.Create(stream, options?.StreamPipeReaderOptions);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await pipeReader.ReadAsync(cancellationToken);
                    var buffer = result.Buffer;
                    while (ScanForJsonDelimitedObject(ref buffer, options, out var payload))
                    {
                        var obj =
                            deserializer != null
                                ? deserializer(payload)
                                : ProcessObject<TValue>(payload, options?.JsonSerializerOptions);
                        yield return obj;
                    }
                    pipeReader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await pipeReader.CompleteAsync();
            }
        }

        /// <summary>
        /// Use this method to deserialize objects from stream and receive it in return value.
        /// </summary>
        /// <param name="stream">Stream to be read from</param>
        /// <param name="options">Options for PipeReder and JsonSerializer</param>
        /// <param name="deserializer">Inject custom deserialized from ReadOnlySequence to TValue</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="TValue">The type will be passed to JsonSerializer.Deserialize method</typeparam>
        /// <returns>List of TValue object</returns>
        public static async Task<List<TValue>> DeserializeAsync<TValue>(
            Stream stream,
            MultiContentJsonReaderOptions? options = null,
            Func<ReadOnlySequence<byte>, TValue>? deserializer = null,
            CancellationToken cancellationToken = default
        )
        {
            var retval = new List<TValue>();
            var _ = await TryDeserializeAsync<TValue>(
                stream,
                retval,
                options,
                deserializer,
                cancellationToken
            );
            return retval;
        }

        /// <summary>
        /// Use this method when you own provided 'list' parameter (eg. from the object pool)
        /// </summary>
        /// <param name="stream">Stream to be read from</param>
        /// <param name="list">List of object to be fill with data</param>
        /// <param name="options">Options for PipeReder and JsonSerializer</param>
        /// <param name="deserializer">Inject custom deserialized from ReadOnlySequence to TValue</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="TValue">The type will be passed to JsonSerializer.Deserializes method</typeparam>
        /// <returns>TRUE if method deserialized at least one object</returns>
        public static async Task<bool> TryDeserializeAsync<TValue>(
            Stream stream,
            IList<TValue> list,
            MultiContentJsonReaderOptions? options = null,
            Func<ReadOnlySequence<byte>, TValue>? deserializer = null,
            CancellationToken cancellationToken = default
        )
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }
            bool deserializedAny = false;
            var pipeReader = PipeReader.Create(stream, options?.StreamPipeReaderOptions);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await pipeReader.ReadAsync(cancellationToken);
                    var buffer = result.Buffer;
                    while (ScanForJsonDelimitedObject(ref buffer, options, out var payload))
                    {
                        var obj =
                            deserializer != null
                                ? deserializer(payload)
                                : ProcessObject<TValue>(payload, options?.JsonSerializerOptions);
                        if (obj != null)
                        {
                            list.Add(obj!);
                            deserializedAny = true;
                        }
                    }
                    pipeReader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await pipeReader.CompleteAsync();
            }
            return deserializedAny;
        }

        /// <summary>
        /// The method check if buffer contains enough data with the whole json object
        /// </summary>
        /// <param name="buffer">data from stream or pipe</param>
        /// <param name="options">options which determines object scan strategy</param>
        /// <param name="obj">sliced buffer to the exact json object</param>
        /// <returns>true if found full json object in buffer, otherwise false</returns>
        private static bool ScanForJsonDelimitedObject(
            ref ReadOnlySequence<byte> buffer,
            MultiContentJsonReaderOptions? options,
            out ReadOnlySequence<byte> obj
        )
        {
            obj = default;
            if (buffer.IsEmpty)
            {
                return false;
            }

            if (!ApproachToObject(ref buffer)) //no json object ahead ?
            {
                obj = default;
                return false;
            }
            var position = GetPosition(buffer, options);

            if (position == null)
            {
                obj = default;
                return false;
            }

            if (buffer.GetOffset(position.Value) >= buffer.GetOffset(buffer.End))
            {
                obj = buffer;
                buffer = buffer.Slice(buffer.End);
            }
            else
            {
                obj = buffer.Slice(0, position.Value);
                var offset =
                    buffer.Slice(buffer.GetPosition(0, position.Value), 1).FirstSpan[0] == '\n'
                        ? 1
                        : 0;
                buffer = buffer.Slice(buffer.GetPosition(offset, position.Value));
            }

            return true;
        }

        private static SequencePosition? GetPosition(
            ReadOnlySequence<byte> buffer,
            MultiContentJsonReaderOptions? options
        )
        {
            if (options?.ObjectScanStrategy == ObjectScanStrategyEnum.NewLine)
            {
                return buffer.PositionOf((byte)'\n');
            }
            var rdr = new Utf8JsonReader(buffer, isFinalBlock: false, state: default);
            if (rdr.Read())
            {
                if (rdr.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("Invalid JSON, must start with an object");
                }

                if (rdr.TrySkip()) //if not then object does not have an end yet, load more sequences
                {
                    return rdr.Position;
                }
            }
            return null;
        }

        private static bool ApproachToObject(ref ReadOnlySequence<byte> buffer)
        {
            if (buffer.FirstSpan[0] != '{')
            {
                SequencePosition? position = buffer.PositionOf((byte)'{');
                if (position != null)
                {
                    buffer = buffer.Slice(position!.Value);
                }
            }
            return buffer.FirstSpan[0] == '{';
        }

        private static TValue? ProcessObject<TValue>(
            ReadOnlySequence<byte> payload,
            JsonSerializerOptions? options
        )
        {
            var innerRdr = new Utf8JsonReader(payload, isFinalBlock: true, state: default);
            var obj = JsonSerializer.Deserialize<TValue>(ref innerRdr, options);
            return obj;
        }
    }
}
