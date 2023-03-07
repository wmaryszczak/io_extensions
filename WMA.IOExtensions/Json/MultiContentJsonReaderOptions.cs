using System.IO.Pipelines;
using System.Text.Json;

namespace WMA.IOExtensions.Json
{
  public enum ObjectScanStrategyEnum
  {
    /// <summary>
    /// Use Utf8JsonReader.TrySkip method to determine boundaries for single json object to be deserialized.
    /// So each json object does not have to be in separated line
    /// </summary>
    JsonTrySkip = 0,
    /// <summary>
    /// Use new line char separator to determine boundaries for single json object to be deserialized.
    /// So each json object shoud be in separated line
    /// </summary>
    NewLine
  }


  public class MultiContentJsonReaderOptions
  {
    public ObjectScanStrategyEnum ObjectScanStrategy { get; set; }
    public StreamPipeReaderOptions? StreamPipeReaderOptions { get; set; }
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }
  }
}
