using Windows.ApplicationModel;

namespace Borderless.App;

/// <summary>
/// Compile-time distribution channel: GitHub Releases vs Microsoft Store.
/// Set with <c>-p:Distribution=GitHub</c> (default) or <c>-p:Distribution=Store</c>.
/// </summary>
public static class AppDistribution
{
#if DISTRIBUTION_STORE
    public const string Channel = "Store";

    public static bool UsesStoreUpdates => true;

    public static bool UsesGitHubUpdates => false;
#else
    public const string Channel = "GitHub";

    public static bool UsesStoreUpdates => false;

    public static bool UsesGitHubUpdates => true;
#endif
}

/// <summary>Runtime MSIX / Store package detection.</summary>
public static class PackagedApp
{
    private static readonly Lazy<bool> IsPackagedLazy = new(DetectIsPackaged);

    public static bool IsPackaged => IsPackagedLazy.Value;

    public static Version? TryGetPackageVersion()
    {
        if (!IsPackaged)
        {
            return null;
        }

        try
        {
            var v = Package.Current.Id.Version;
            return new Version(v.Major, v.Minor, v.Build, v.Revision);
        }
        catch
        {
            return null;
        }
    }

    private static bool DetectIsPackaged()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
