using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Xunit;

namespace Weather.PerformanceTests;

/// <summary>
/// Runs the benchmark suite on demand. It is skipped unless <c>WEATHER_RUN_BENCHMARKS=1</c>, because a
/// statistically meaningful run takes minutes and has no place in pull request feedback.
/// <code>
/// $env:WEATHER_RUN_BENCHMARKS = "1"
/// dotnet test tests/Weather.PerformanceTests -c Release --filter FullyQualifiedName~BenchmarkRun
/// </code>
/// </summary>
public sealed class BenchmarkRun
{
    [Fact]
    public void Run()
    {
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable("WEATHER_RUN_BENCHMARKS"), "1", StringComparison.Ordinal),
            "Set WEATHER_RUN_BENCHMARKS=1 to run the benchmark suite.");

        // The in-process toolchain keeps the run inside the test host: a test project has no entry point
        // for BenchmarkDotNet's default out-of-process runner to build against.
        ManualConfig config = ManualConfig.CreateMinimumViable()
            .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddLogger(ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        Summary summary = BenchmarkRunner.Run<WeatherBenchmarks>(config);

        Assert.False(summary.HasCriticalValidationErrors);
    }
}
