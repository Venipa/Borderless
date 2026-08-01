using System.Diagnostics;
using Borderless.App.Models;

namespace Borderless.App.Services;

/// <summary>
/// Snapshots visible processes for typeahead suggestions (background-thread safe).
/// </summary>
public sealed class ProcessCatalogService
{
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly string _ownProcessName;
    private readonly string _ownExecutableName;

    public ProcessCatalogService()
    {
        using var current = Process.GetCurrentProcess();
        _ownProcessName = current.ProcessName;
        _ownExecutableName = current.ProcessName + ".exe";
    }

    public Task<IReadOnlyList<ProcessSuggestion>> GetRunningProcessesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetRunningProcesses(cancellationToken), cancellationToken);
    }

    public IReadOnlyList<ProcessSuggestion> GetRunningProcesses(
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProcessSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (IsOwnProcess(process))
                {
                    continue;
                }

                var title = process.MainWindowTitle?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var executableName = GetExecutableName(process);
                if (string.IsNullOrWhiteSpace(executableName))
                {
                    continue;
                }

                var key = $"{executableName}|{title}";
                if (!seen.Add(key))
                {
                    continue;
                }

                results.Add(new ProcessSuggestion
                {
                    WindowTitle = title,
                    ExecutableName = executableName
                });
            }
            catch (InvalidOperationException)
            {
                // Process exited.
            }
            catch
            {
                // Access denied / transient failures.
            }
            finally
            {
                process.Dispose();
            }
        }

        results.Sort(static (left, right) =>
        {
            var exe = string.Compare(left.ExecutableName, right.ExecutableName, StringComparison.OrdinalIgnoreCase);
            return exe != 0
                ? exe
                : string.Compare(left.WindowTitle, right.WindowTitle, StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }

    private bool IsOwnProcess(Process process)
    {
        if (process.Id == _ownProcessId)
        {
            return true;
        }

        try
        {
            return string.Equals(process.ProcessName, _ownProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetExecutableName(Process process)
    {
        // Avoid MainModule — expensive and often throws; ProcessName is enough for matching.
        try
        {
            var name = process.ProcessName;
            return string.IsNullOrWhiteSpace(name) ? null : name + ".exe";
        }
        catch
        {
            return null;
        }
    }
}
