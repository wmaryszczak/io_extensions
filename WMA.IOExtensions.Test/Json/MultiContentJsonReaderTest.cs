using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using IOExtensions.Test.Models;
using WMA.IOExtensions.Json;

namespace WMA.IOExtensions.Test.Json
{
    public class MultiContentJsonReaderTest
    {
        [Theory]
        [InlineData("./Examples/single.json", 1)]
        [InlineData("./Examples/multicontent.json", 6)]
        [InlineData("./Examples/corruptedmulticontent.json", 2)]
        public async Task Should_DeserializeAsyncEnumerable(
            string filePath,
            int extectedElementCount
        )
        {
            using var stream = new FileStream(
                filePath!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                100,
                FileOptions.SequentialScan
            );
            List<HotelModel> subject = new();
            await foreach (
                var hotel in MultiContentJsonReader.DeserializeAsyncEnumerable<HotelModel>(stream)
            )
            {
                if (hotel != null)
                {
                    subject.Add(hotel!);
                }
            }
            Assert.Equal(extectedElementCount, subject.Count);
        }

        [Theory]
        [InlineData("./Examples/single.json", 1)]
        [InlineData("./Examples/multicontent.json", 6)]
        [InlineData("./Examples/corruptedmulticontent.json", 2)]
        public async Task Should_DeserializeAsync(string filePath, int extectedElementCount)
        {
            using var stream = new FileStream(
                filePath!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                100,
                FileOptions.SequentialScan
            );
            List<HotelModel> subject = await MultiContentJsonReader.DeserializeAsync<HotelModel>(
                stream
            );
            Assert.Equal(extectedElementCount, subject.Count);
        }

        [Theory]
        [InlineData("./Examples/multicontent.json", 6)]
        public async Task Should_DeserializeAsync_NewLine_ObjectScanStrategyEnum(
            string filePath,
            int extectedElementCount
        )
        {
            using var stream = new FileStream(
                filePath!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                100,
                FileOptions.SequentialScan
            );
            List<HotelModel> subject = await MultiContentJsonReader.DeserializeAsync<HotelModel>(
                stream,
                new MultiContentJsonReaderOptions
                {
                    ObjectScanStrategy = ObjectScanStrategyEnum.NewLine,
                }
            );
            Assert.Equal(extectedElementCount, subject.Count);
        }

        [Fact]
        public async Task Should_DeserializeAsync_EachLine_With_Custom_Deserializer()
        {
            var filePath = "./Examples/multicontent.json";
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                100,
                FileOptions.SequentialScan
            );
            List<string> subject = await MultiContentJsonReader.DeserializeAsync<string>(
                stream,
                new MultiContentJsonReaderOptions
                {
                    ObjectScanStrategy = ObjectScanStrategyEnum.NewLine,
                },
                DecodeToUtf8
            );
            Assert.Equal(6, subject.Count);
            foreach (var el in subject)
            {
                Assert.StartsWith("{", el);
                Assert.EndsWith("}", el);
            }
        }

        private string DecodeToUtf8(ReadOnlySequence<byte> sequence)
        {
            return Encoding.UTF8.GetString(sequence);
        }

        [Fact]
        public async Task Should_Throw_Exception_Deserializing_Multiline_Object_With_NewLine_ObjectScanStrategyEnum()
        {
            var filePath = "./Examples/single.json";
            using var stream = new FileStream(
                filePath!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                100,
                FileOptions.SequentialScan
            );
            await Assert.ThrowsAsync<JsonException>(async () =>
            {
                await MultiContentJsonReader.DeserializeAsync<HotelModel>(
                    stream,
                    new MultiContentJsonReaderOptions
                    {
                        ObjectScanStrategy = ObjectScanStrategyEnum.NewLine,
                    }
                );
            });
        }
    }
}
