using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Borderless.App.Helpers;
using Borderless.App.Localization;

namespace Borderless.App.Services;

public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; init; }

    public Version LocalVersion { get; init; } = new(0, 0, 0, 0);

    public Version? RemoteVersion { get; init; }

    public string? TagName { get; init; }

    public string? SetupDownloadUrl { get; init; }

    public string? ZipDownloadUrl { get; init; }

    public string? ReleaseHtmlUrl { get; init; }

    public string? ErrorMessage { get; init; }

    public string? StatusMessage { get; init; }
}

/// <summary>
/// Checks GitHub Releases for newer versions and downloads the setup/zip asset.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public UpdateService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{AppMetadata.Product}/{AppMetadata.GetLocalVersionString()} (+{AppMetadata.RepositoryUrl})");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var local = AppMetadata.GetLocalVersion();

        try
        {
            using var response = await _http.GetAsync(AppMetadata.UpdateReleasesApiUrl, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult
                {
                    LocalVersion = local,
                    StatusMessage = Loc.Get("UpdateNoneAvailable"),
                    ReleaseHtmlUrl = AppMetadata.UpdateReleasesPageUrl
                };
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new UpdateCheckResult
                {
                    LocalVersion = local,
                    ErrorMessage = Loc.Get("UpdateCheckFailed")
                };
            }

            if (!AppMetadata.TryParseVersion(release.TagName, out var remote))
            {
                return new UpdateCheckResult
                {
                    LocalVersion = local,
                    TagName = release.TagName,
                    ErrorMessage = Loc.Get("UpdateCheckFailed")
                };
            }

            var setupUrl = FindAssetUrl(release, "-win-x64-setup.exe");
            var zipUrl = FindAssetUrl(release, "-win-x64.zip");
            var available = remote > local;

            return new UpdateCheckResult
            {
                IsUpdateAvailable = available,
                LocalVersion = local,
                RemoteVersion = remote,
                TagName = release.TagName,
                SetupDownloadUrl = setupUrl,
                ZipDownloadUrl = zipUrl,
                ReleaseHtmlUrl = release.HtmlUrl ?? AppMetadata.UpdateReleasesPageUrl,
                StatusMessage = available
                    ? string.Format(Loc.Get("UpdateAvailableFormat"), remote, local)
                    : Loc.Get("UpdateNoneAvailable")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                LocalVersion = local,
                ErrorMessage = string.Format(Loc.Get("UpdateCheckErrorFormat"), ex.Message)
            };
        }
    }

    public async Task ApplyUpdateAsync(UpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var url = update.SetupDownloadUrl ?? update.ZipDownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(Loc.Get("UpdateNoAsset"));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = update.SetupDownloadUrl is not null
                    ? $"Borderless-{update.RemoteVersion}-win-x64-setup.exe"
                    : $"Borderless-{update.RemoteVersion}-win-x64.zip";
            }

            var targetPath = Path.Combine(Path.GetTempPath(), fileName);
            await using (var remote = await _http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false))
            await using (var local = File.Create(targetPath))
            {
                await remote.CopyToAsync(local, cancellationToken).ConfigureAwait(false);
            }

            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // Silent install; Inno [Run] Verb=runas starts the app elevated.
                // NORESTARTAPPLICATIONS avoids a non-elevated auto-restart of the closed process.
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /NORESTARTAPPLICATIONS",
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true
                });
            }

            await UiDispatch.InvokeAsync(() => Application.Current.Shutdown()).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _http.Dispose();
        _gate.Dispose();
    }

    private static string? FindAssetUrl(GitHubRelease release, string nameSuffix)
    {
        return release.Assets?
            .FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name)
                && a.Name.EndsWith(nameSuffix, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
            ?.BrowserDownloadUrl;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
