# io_extensions

Project contains extensions to standard .net implementation, which provide the following functionalities:

* `MultiContentJsonReader` provides reading idepedent json object from stream which is not a valid json array.
  It can be new line delimited or not delimited at all.

## How to use it

## How to run

run unit tests

```
dotnet test
```

run Benchmark.Net

```
cd IOExtensions.Test
dotnet run -c Release -- --job short --runtimes net7.0 --filter "*"
```

## Benchmark resukts

* `WMA.IOExtensions.Json` namespace

| Method                            |      Mean |     Error |    StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
|-----------------------------------|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| Baseline                          | 182.30 us | 191.87 us | 10.517 us |  1.00 |    0.00 |  24.02 KB |        1.00 |
| Should_DeserializeAsyncEnumerable |  75.92 us |  36.34 us |  1.992 us |  0.42 |    0.02 |  11.65 KB |        0.49 |
| Should_DeserializeAsync           |  79.42 us |  55.98 us |  3.069 us |  0.44 |    0.04 |  11.61 KB |        0.48 |
| Should_DeserializeAsync_NewLine   |  55.59 us |  69.33 us |  3.800 us |  0.31 |    0.03 |  11.61 KB |        0.48 |
