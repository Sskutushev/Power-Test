using Microsoft.Playwright;
using Xunit;

namespace Weather.E2ETests;

/// <summary>
/// Regenerates the PWA icons from the SVG source. Skipped unless <c>WEATHER_GENERATE_ICONS=1</c>.
/// <para>
/// Chromium is already a dependency of this project, so it doubles as the rasteriser rather than adding
/// an image library to the solution for four files that change once a year.
/// </para>
/// <code>
/// $env:WEATHER_GENERATE_ICONS = "1"
/// dotnet test tests/Weather.E2ETests --filter FullyQualifiedName~PwaIconGeneration
/// </code>
/// </summary>
public sealed class PwaIconGeneration
{
    private static readonly (string Name, int Size, bool Maskable)[] Icons =
    [
        ("icon-192.png", 192, false),
        ("icon-512.png", 512, false),
        ("icon-maskable-512.png", 512, true)
    ];

    [Fact]
    public async Task Generate()
    {
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable("WEATHER_GENERATE_ICONS"), "1", StringComparison.Ordinal),
            "Set WEATHER_GENERATE_ICONS=1 to regenerate the PWA icons.");

        string root = RepositoryRoot();
        string source = Path.Combine(root, "src", "Weather.Web", "wwwroot", "favicon.svg");
        string target = Path.Combine(root, "src", "Weather.Web", "wwwroot", "icons");
        Directory.CreateDirectory(target);

        string svg = await File.ReadAllTextAsync(source, TestContext.Current.CancellationToken);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        foreach ((string name, int size, bool maskable) in Icons)
        {
            // A maskable icon must survive being cropped to a circle, so the artwork is inset and the
            // background is extended to the full square.
            double inset = maskable ? 0.62 : 0.88;

            IPage page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = size, Height = size }
            });

            await page.SetContentAsync($$"""
                <!DOCTYPE html>
                <html><head><style>
                  html, body { margin: 0; width: {{size}}px; height: {{size}}px; }
                  body { display: grid; place-items: center; background: #0b1418; }
                  svg { width: {{(int)(size * inset)}}px; height: {{(int)(size * inset)}}px; }
                </style></head>
                <body>{{svg}}</body></html>
                """);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(target, name),
                OmitBackground = false
            });

            await page.CloseAsync();
        }

        Directory.EnumerateFiles(target, "*.png").Should_HaveAtLeast(Icons.Length);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WeatherApp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

internal static class IconAssertions
{
    public static void Should_HaveAtLeast(this IEnumerable<string> files, int expected)
    {
        int count = files.Count();

        if (count < expected)
        {
            throw new InvalidOperationException($"Expected at least {expected} icons, found {count}.");
        }
    }
}
