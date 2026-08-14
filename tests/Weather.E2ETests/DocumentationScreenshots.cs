using Microsoft.Playwright;
using Xunit;

namespace Weather.E2ETests;

/// <summary>
/// Captures the README screenshots from a running instance. It is skipped unless
/// <c>WEATHER_SCREENSHOT_URL</c> points at one, so a normal test run never depends on a live provider.
/// <code>
/// docker compose up -d
/// $env:WEATHER_SCREENSHOT_URL = "http://127.0.0.1:8080"
/// dotnet test tests/Weather.E2ETests --filter FullyQualifiedName~DocumentationScreenshots
/// </code>
/// </summary>
public sealed class DocumentationScreenshots
{
    private static readonly (string Name, string? Theme, int Width, int Height)[] Shots =
    [
        ("dashboard-light", "aurora", 1440, 1100),
        ("dashboard-dark", "midnight", 1440, 1100),
        ("dashboard-console", "console", 1440, 1100),
        ("dashboard-mobile", "aurora", 390, 900)
    ];

    [Fact]
    public async Task Capture()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("WEATHER_SCREENSHOT_URL");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl), "WEATHER_SCREENSHOT_URL is not set.");

        string output = Path.Combine(RepositoryRoot(), "docs", "screenshots");
        Directory.CreateDirectory(output);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        foreach ((string name, string? theme, int width, int height) in Shots)
        {
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = width, Height = height },
                Locale = "ru-RU",
                // 1x keeps the committed PNGs around a megabyte instead of four; they are README
                // illustrations, not print assets.
                DeviceScaleFactor = 1,
                // Animations off keeps the captures byte-stable between runs.
                ReducedMotion = ReducedMotion.Reduce
            });

            IPage page = await context.NewPageAsync();
            await page.GotoAsync(baseUrl!);
            page.SetDefaultTimeout(90_000);
            await page.Locator(".current-weather").WaitForAsync();

            if (theme is not null)
            {
                await page.EvaluateAsync("theme => document.documentElement.setAttribute('data-theme', theme)", theme);
            }

            await page.Locator(".map-canvas, .map-fallback, .empty-note").First.WaitForAsync();
            await page.WaitForTimeoutAsync(2500);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(output, $"{name}.png"),
                FullPage = true
            });
        }
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
