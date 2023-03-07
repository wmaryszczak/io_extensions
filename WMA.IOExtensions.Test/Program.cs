namespace IOExtensions.Test;

public class Program
{
  public static void Main(string[] args)
  {
    var config = ManualConfig.CreateEmpty()
      .AddLogger(ConsoleLogger.Default)
      .AddColumnProvider(DefaultColumnProviders.Instance)
      .AddDiagnoser(MemoryDiagnoser.Default)
      .AddAnalyser(EnvironmentAnalyser.Default,
            OutliersAnalyser.Default,
            MinIterationTimeAnalyser.Default,
            MultimodalDistributionAnalyzer.Default,
            RuntimeErrorAnalyser.Default)
      .AddValidator(BaselineValidator.FailOnError,
            SetupCleanupValidator.FailOnError,
            JitOptimizationsValidator.FailOnError,
            RunModeValidator.FailOnError)
      ;
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
  }
}
