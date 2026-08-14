using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Weather.Infrastructure.Tests")]
[assembly: InternalsVisibleTo("Weather.Architecture.Tests")]

// Benchmarks measure the real deserialise-and-map path, which is internal on purpose. Exposing it to the
// benchmark assembly is preferable to making the provider DTOs public just to time them.
[assembly: InternalsVisibleTo("Weather.PerformanceTests")]
