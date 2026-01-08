using BenchmarkDotNet.Running;
using MarketDataCollector.Benchmarks;

// Run all benchmarks
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
