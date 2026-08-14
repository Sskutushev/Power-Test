using FluentAssertions;
using Xunit;

namespace Weather.Infrastructure.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Infrastructure_test_project_is_wired()
    {
        typeof(Weather.Infrastructure.AssemblyMarker).Assembly.GetName().Name.Should().Be("Weather.Infrastructure");
    }
}
