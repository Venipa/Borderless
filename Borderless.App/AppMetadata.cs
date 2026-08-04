using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Borderless.App;

/// <summary>
/// Runtime app metadata: version + GitHub update endpoints (update.json overrides BuildInfo).
/// </summary>
public static class AppMetadata
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static UpdateEndpointConfig? _endpoints;

    public static string Author => BuildInfo.Author;

    public static string Product => BuildInfo.Product;

    public static string GitHubRepository => Endpoints.Repository ?? BuildInfo.GitHubRepository;

    public static string RepositoryUrl => Endpoints.RepositoryUrl ?? BuildInfo.RepositoryUrl;

    public static string UpdateReleasesApiUrl => Endpoints.ReleasesApiUrl ?? BuildInfo.UpdateReleasesApiUrl;

    public static string UpdateReleasesPageUrl => Endpoints.ReleasesPageUrl ?? BuildInfo.UpdateReleasesPageUrl;

    public static Version GetLocalVersion()
    {
        var packageVersion = PackagedApp.TryGetPackageVersion();
        if (packageVersion is not null)
        {
            return packageVersion;
        }

        var fromFile = TryReadVersionFile();
        if (fromFile is not null)
        {
            return fromFile;
        }

        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (TryParseVersion(informational, out var parsed))
        {
            return parsed;
        }

        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
    }

    public static string GetLocalVersionString() => GetLocalVersion().ToString();

    private static UpdateEndpointConfig Endpoints => _endpoints ??= LoadEndpoints();

    private static UpdateEndpointConfig LoadEndpoints()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "update.json");
            if (!File.Exists(path))
            {
                return new UpdateEndpointConfig();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UpdateEndpointConfig>(json, JsonOptions)
                ?? new UpdateEndpointConfig();
        }
        catch
        {
            return new UpdateEndpointConfig();
        }
    }

    private static Version? TryReadVersionFile()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return TryParseVersion(text, out var version) ? version : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        var plus = trimmed.IndexOf('+');
        if (plus >= 0)
        {
            trimmed = trimmed[..plus];
        }

        return Version.TryParse(trimmed, out version!);
    }

    private sealed class UpdateEndpointConfig
    {
        [JsonPropertyName("repository")]
        public string? Repository { get; set; }

        [JsonPropertyName("releasesApiUrl")]
        public string? ReleasesApiUrl { get; set; }

        [JsonPropertyName("releasesPageUrl")]
        public string? ReleasesPageUrl { get; set; }

        [JsonPropertyName("repositoryUrl")]
        public string? RepositoryUrl { get; set; }
    }
}
