using FluentAssertions;
using Xunit;

namespace Weather.Architecture.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Architecture_test_project_is_wired()
    {
        typeof(Weather.Domain.AssemblyMarker).Assembly.GetName().Name.Should().Be("Weather.Domain");
    }
}
