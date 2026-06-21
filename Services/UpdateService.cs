using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.IO;

namespace RecipeManager.Services;

public sealed record UpdateCheckResult(
    bool IsConfigured,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion = null,
    string? ReleaseUrl = null,
    string? InstallerUrl = null,
    long? InstallerSize = null,
    string? Error = null);

public sealed class UpdateService
{
    private const string DefaultRepository = "sanhai92/RecipeManager";

    public string CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.1";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var repository = GetRepository();
        if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
            return new UpdateCheckResult(false, false, CurrentVersion);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RecipeManager", CurrentVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await client.GetAsync($"https://api.github.com/repos/{repository}/releases/latest", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? string.Empty;
            var releaseUrl = json.RootElement.GetProperty("html_url").GetString();
            string? installerUrl = null;
            long? installerSize = null;
            if (json.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? string.Empty;
                    if (!name.StartsWith("RecipeManager-Setup-", StringComparison.OrdinalIgnoreCase)
                        || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    installerUrl = asset.GetProperty("browser_download_url").GetString();
                    installerSize = asset.TryGetProperty("size", out var size) ? size.GetInt64() : null;
                    break;
                }
            }
            var comparableTag = tag.Split('-', '+')[0];
            var available = Version.TryParse(comparableTag, out var latest)
                && Version.TryParse(CurrentVersion, out var current)
                && latest > current;
            return new UpdateCheckResult(true, available, CurrentVersion, tag, releaseUrl, installerUrl, installerSize);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(true, false, CurrentVersion, Error: ex.Message);
        }
    }

    public async Task<string> DownloadInstallerAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!update.IsUpdateAvailable || string.IsNullOrWhiteSpace(update.InstallerUrl))
            throw new InvalidOperationException("This release does not contain a Recipe Manager installer.");
        if (!Uri.TryCreate(update.InstallerUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && !uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The update download address is not a trusted GitHub URL.");

        var updateFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecipeManager", "Updates");
        Directory.CreateDirectory(updateFolder);
        var version = string.Concat((update.LatestVersion ?? "latest").Where(x => char.IsLetterOrDigit(x) || x is '.' or '-'));
        var destination = Path.Combine(updateFolder, $"RecipeManager-Setup-{version}.exe");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RecipeManager", CurrentVersion));
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? update.InstallerSize;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total is > 0) progress?.Report(received * 100d / total.Value);
        }
        if (received == 0) throw new InvalidOperationException("The downloaded installer is empty.");
        progress?.Report(100);
        return destination;
    }

    private static string GetRepository()
    {
        var configuredRepository = Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "UpdateRepository")?.Value;

        return string.IsNullOrWhiteSpace(configuredRepository)
            ? DefaultRepository
            : configuredRepository;
    }
}
