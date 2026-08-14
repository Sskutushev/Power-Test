using FluentAssertions;
using Xunit;

namespace Weather.PerformanceTests;

public sealed class SmokeTests
{
    [Fact]
    public void Performance_test_project_is_wired()
    {
        typeof(Weather.Application.AssemblyMarker).Assembly.GetName().Name.Should().Be("Weather.Application");
    }
}
