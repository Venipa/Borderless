using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Interop;
using Borderless.App.Helpers;
using Borderless.App.Localization;
#if DISTRIBUTION_STORE
using Windows.Services.Store;
#endif

namespace Borderless.App.Services;

public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; init; }

    public Version LocalVersion { get; init; } = new(0, 0, 0, 0);

    public Version? RemoteVersion { get; init; }

    public string? TagName { get; init; }

    public string? ReleaseName { get; init; }

    /// <summary>Raw GitHub release body (markdown, not rendered).</summary>
    public string? ReleaseBody { get; init; }

    public string? SetupDownloadUrl { get; init; }

    public string? ZipDownloadUrl { get; init; }

    public string? MsixDownloadUrl { get; init; }

    public string? ReleaseHtmlUrl { get; init; }

    public string? ErrorMessage { get; init; }

    public string? StatusMessage { get; init; }
}

/// <summary>
/// Channel-specific updater: GitHub Releases (setup / zip / MSIX) or Microsoft Store.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _pendingGate = new();
    private string? _pendingInstallerPath;
    private bool _disposed;

#if DISTRIBUTION_STORE
    private IReadOnlyList<StorePackageUpdate>? _pendingStoreUpdates;
#endif

    public UpdateService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{AppMetadata.Product}/{AppMetadata.GetLocalVersionString()} (+{AppMetadata.RepositoryUrl}; {AppDistribution.Channel})");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string? PendingInstallerPath
    {
        get
        {
            lock (_pendingGate)
            {
                return _pendingInstallerPath;
            }
        }
    }

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
#if DISTRIBUTION_STORE
        return CheckStoreUpdatesAsync(cancellationToken);
#else
        return CheckGitHubUpdatesAsync(cancellationToken);
#endif
    }

    public async Task ApplyUpdateAsync(UpdateCheckResult update, CancellationToken cancellationToken = default)
    {
#if DISTRIBUTION_STORE
        await ApplyStoreUpdateAsync(cancellationToken).ConfigureAwait(false);
#else
        var path = await DownloadUpdateAsync(update, cancellationToken).ConfigureAwait(false);
        LaunchInstaller(path, shutdownApp: true);
#endif
    }

    public async Task<string> DownloadUpdateAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken = default)
    {
#if DISTRIBUTION_STORE
        throw new InvalidOperationException(Loc.Get("UpdateStoreNoDownload"));
#else
        ArgumentNullException.ThrowIfNull(update);

        var url = ResolveGitHubDownloadUrl(update);
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
                fileName = BuildFallbackFileName(update, url);
            }

            var targetPath = Path.Combine(Path.GetTempPath(), fileName);
            await using (var remote = await _http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false))
            await using (var local = File.Create(targetPath))
            {
                await remote.CopyToAsync(local, cancellationToken).ConfigureAwait(false);
            }

            return targetPath;
        }
        finally
        {
            _gate.Release();
        }
#endif
    }

    public void ScheduleInstallOnExit(string installerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);

        lock (_pendingGate)
        {
            _pendingInstallerPath = installerPath;
        }
    }

    public void TryLaunchPendingInstaller()
    {
        string? path;
        lock (_pendingGate)
        {
            path = _pendingInstallerPath;
            _pendingInstallerPath = null;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        LaunchInstaller(path, shutdownApp: false, launchAppAfterInstall: false);
    }

    public void LaunchInstaller(string installerPath, bool shutdownApp, bool launchAppAfterInstall = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);

        if (installerPath.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)
            || installerPath.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase)
            || installerPath.EndsWith(".appx", StringComparison.OrdinalIgnoreCase)
            || installerPath.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase))
        {
            LaunchMsixInstaller(installerPath);
        }
        else if (installerPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // Silent install. Inno [Run] starts the app unless /NORUN is passed.
            // NORESTARTAPPLICATIONS avoids a non-elevated auto-restart of the closed process.
            var arguments = "/SILENT /CLOSEAPPLICATIONS /NORESTARTAPPLICATIONS";
            if (!launchAppAfterInstall)
            {
                arguments += " /NORUN";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });
        }

        if (shutdownApp)
        {
            _ = UiDispatch.InvokeAsync(() => Application.Current.Shutdown());
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

#if DISTRIBUTION_STORE
    private async Task<UpdateCheckResult> CheckStoreUpdatesAsync(CancellationToken cancellationToken)
    {
        var local = AppMetadata.GetLocalVersion();

        try
        {
            var context = await CreateStoreContextAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(cancellationToken)
                .ConfigureAwait(false);
            _pendingStoreUpdates = updates;

            var available = updates.Count > 0;
            return new UpdateCheckResult
            {
                IsUpdateAvailable = available,
                LocalVersion = local,
                RemoteVersion = available ? null : local,
                ReleaseHtmlUrl = AppMetadata.UpdateReleasesPageUrl,
                StatusMessage = available
                    ? Loc.Get("UpdateStoreAvailable")
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

    private async Task ApplyStoreUpdateAsync(CancellationToken cancellationToken)
    {
        var context = await CreateStoreContextAsync().ConfigureAwait(false);
        var updates = _pendingStoreUpdates;
        if (updates is null || updates.Count == 0)
        {
            updates = await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(cancellationToken)
                .ConfigureAwait(false);
        }

        if (updates.Count == 0)
        {
            return;
        }

        _ = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<StoreContext> CreateStoreContextAsync()
    {
        var context = StoreContext.GetDefault();
        await UiDispatch.InvokeAsync(() =>
        {
            var window = Application.Current?.MainWindow;
            if (window is null)
            {
                return;
            }

            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            WinRT.Interop.InitializeWithWindow.Initialize(context, hwnd);
        }).ConfigureAwait(false);

        return context;
    }
#else
    private async Task<UpdateCheckResult> CheckGitHubUpdatesAsync(CancellationToken cancellationToken)
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
            var msixUrl = FindAssetUrl(release, "-win-x64.msix");
            var available = remote > local;

            return new UpdateCheckResult
            {
                IsUpdateAvailable = available,
                LocalVersion = local,
                RemoteVersion = remote,
                TagName = release.TagName,
                ReleaseName = release.Name,
                ReleaseBody = release.Body,
                SetupDownloadUrl = setupUrl,
                ZipDownloadUrl = zipUrl,
                MsixDownloadUrl = msixUrl,
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

    private static string? ResolveGitHubDownloadUrl(UpdateCheckResult update)
    {
        // Packaged (sideloaded MSIX) prefers MSIX; classic install prefers Inno setup.
        if (PackagedApp.IsPackaged)
        {
            return FirstNonEmpty(update.MsixDownloadUrl, update.SetupDownloadUrl, update.ZipDownloadUrl);
        }

        return FirstNonEmpty(update.SetupDownloadUrl, update.ZipDownloadUrl, update.MsixDownloadUrl);
    }

    private static string BuildFallbackFileName(UpdateCheckResult update, string url)
    {
        if (url.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
        {
            return $"Borderless-{update.RemoteVersion}-win-x64.msix";
        }

        if (update.SetupDownloadUrl is not null)
        {
            return $"Borderless-{update.RemoteVersion}-win-x64-setup.exe";
        }

        return $"Borderless-{update.RemoteVersion}-win-x64.zip";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

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
#endif

    private static void LaunchMsixInstaller(string installerPath)
    {
        var fullPath = Path.GetFullPath(installerPath);
        var escaped = fullPath.Replace("'", "''", StringComparison.Ordinal);
        var command =
            $"Add-AppxPackage -Path '{escaped}' -ForceUpdateFromAnyVersion -ErrorAction Stop";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{command}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
