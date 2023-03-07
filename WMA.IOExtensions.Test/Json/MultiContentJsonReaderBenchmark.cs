using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using IOExtensions.Test.Models;
using WMA.IOExtensions.Json;

namespace WMA.IOExtensions.Test.Json
{
  public class MultiContentJsonReaderBenchmark
  {
    private MemoryStream stream;
    private MemoryStream validContentStream;
    List<HotelModel> container = new();
    private MultiContentJsonReaderOptions options;

    [GlobalSetup]
    public void GlobalSetup()
    {
      this.stream = new MemoryStream();
      this.validContentStream = new MemoryStream();

      CopyContent("./Examples/valid_array.json", this.validContentStream);
      CopyContent("./Examples/multicontent.json", this.stream);

    }

    [IterationSetup]
    public void IterationSetup()
    {
      this.validContentStream.Position = 0;
      this.stream.Position = 0;
      this.options = new MultiContentJsonReaderOptions
      {
        StreamPipeReaderOptions = new StreamPipeReaderOptions(null, 4096, 1024, true)
      };
    }

    private void CopyContent(string path, Stream target)
    {
      using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
      source.CopyTo(target);
    }

    [Benchmark(Baseline = true)]
    public async Task<List<HotelModel>> Baseline()
    {
      await foreach (var hotel in System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<HotelModel>(this.validContentStream))
      {
        if (hotel != null)
        {
          container.Add(hotel!);
        }
      }
      return container;
    }

    [Benchmark]
    public async Task<List<HotelModel>> Should_DeserializeAsyncEnumerable()
    {
      await foreach(var hotel in MultiContentJsonReader.DeserializeAsyncEnumerable<HotelModel>(stream, options))
      {
        if(hotel != null)
        {
          container.Add(hotel!);
        }
      }
      return container;
    }

    [Benchmark]
    public async Task<List<HotelModel>> Should_DeserializeAsync()
    {
      return await MultiContentJsonReader.DeserializeAsync<HotelModel>(stream, options);
    }

    [Benchmark]
    public async Task<List<HotelModel>> Should_DeserializeAsync_NewLine()
    {
      this.options.ObjectScanStrategy = ObjectScanStrategyEnum.NewLine;
      return await MultiContentJsonReader.DeserializeAsync<HotelModel>(stream, options);
    }
  }
}
