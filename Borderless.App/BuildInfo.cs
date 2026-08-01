namespace Borderless.App;

/// <summary>
/// Compile-time defaults for author/repo/update endpoints.
/// CI overrides at publish via <c>update.json</c> next to the exe (preferred by <see cref="AppMetadata"/>).
/// MSBuild can also rewrite these constants by regenerating this file if needed.
/// </summary>
internal static class BuildInfo
{
    public const string Author = "Venipa";

    public const string Product = "Borderless";

    public const string GitHubRepository = "Venipa/Borderless";

    public const string RepositoryUrl = "https://github.com/Venipa/Borderless";

    public const string UpdateReleasesApiUrl = "https://api.github.com/repos/Venipa/Borderless/releases/latest";

    public const string UpdateReleasesPageUrl = "https://github.com/Venipa/Borderless/releases";
}
