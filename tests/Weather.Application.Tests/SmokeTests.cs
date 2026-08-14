using FluentAssertions;
using Xunit;

namespace Weather.Application.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Application_test_project_is_wired()
    {
        typeof(global::Weather.Application.AssemblyMarker).Assembly.GetName().Name.Should().Be("Weather.Application");
    }
}
