using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing workspace templates and session restore.
/// Implements Feature Refinement #51 - Workspace Templates & Session Restore.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService
{
    private static WorkspaceService? _instance;
    private static readonly object _lock = new();

    private const string WorkspacesKey = "Workspaces";
    private const string ActiveWorkspaceKey = "ActiveWorkspace";
    private const string LastSessionKey = "LastSession";
    private const string WorkspacesFolder = "Workspaces";

    private WorkspaceTemplate? _activeWorkspace;
    private SessionState? _lastSession;
    private readonly List<WorkspaceTemplate> _workspaces = new();

    public static WorkspaceService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new WorkspaceService();
                }
            }
            return _instance;
        }
    }

    private WorkspaceService()
    {
        _ = LoadWorkspacesAsync();
    }

    /// <summary>
    /// Gets the active workspace.
    /// </summary>
    public WorkspaceTemplate? ActiveWorkspace => _activeWorkspace;

    /// <summary>
    /// Gets the last session state for restore.
    /// </summary>
    public SessionState? LastSession => _lastSession;

    /// <summary>
    /// Gets all available workspace templates.
    /// </summary>
    public IReadOnlyList<WorkspaceTemplate> Workspaces => _workspaces.AsReadOnly();

    /// <summary>
    /// Loads workspaces from storage.
    /// </summary>
    public async Task LoadWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            // Load workspace list
            if (localSettings.Values.TryGetValue(WorkspacesKey, out var workspacesJson))
            {
                var workspaces = JsonSerializer.Deserialize<List<WorkspaceTemplate>>(workspacesJson?.ToString() ?? "[]");
                if (workspaces != null)
                {
                    _workspaces.Clear();
                    _workspaces.AddRange(workspaces);
                }
            }

            // Add default workspaces if none exist
            if (_workspaces.Count == 0)
            {
                _workspaces.AddRange(GetDefaultWorkspaces());
                await SaveWorkspacesAsync(cancellationToken);
            }

            // Load active workspace
            if (localSettings.Values.TryGetValue(ActiveWorkspaceKey, out var activeId))
            {
                _activeWorkspace = _workspaces.FirstOrDefault(w => w.Id == activeId?.ToString());
            }

            // Load last session
            if (localSettings.Values.TryGetValue(LastSessionKey, out var sessionJson))
            {
                _lastSession = JsonSerializer.Deserialize<SessionState>(sessionJson?.ToString() ?? "{}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error loading workspaces", ex);
        }
    }

    /// <summary>
    /// Saves workspaces to storage.
    /// </summary>
    public async Task SaveWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var json = JsonSerializer.Serialize(_workspaces);
            localSettings.Values[WorkspacesKey] = json;

            if (_activeWorkspace != null)
            {
                localSettings.Values[ActiveWorkspaceKey] = _activeWorkspace.Id;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error saving workspaces", ex);
        }
    }

    /// <summary>
    /// Creates a new workspace template.
    /// </summary>
    public async Task<WorkspaceTemplate> CreateWorkspaceAsync(string name, string description, WorkspaceCategory category, CancellationToken cancellationToken = default)
    {
        var workspace = new WorkspaceTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Pages = new List<WorkspacePage>(),
            WidgetLayout = new Dictionary<string, WidgetPosition>(),
            Filters = new Dictionary<string, string>()
        };

        _workspaces.Add(workspace);
        await SaveWorkspacesAsync(cancellationToken);

        WorkspaceCreated?.Invoke(this, new WorkspaceEventArgs { Workspace = workspace });
        return workspace;
    }

    /// <summary>
    /// Updates an existing workspace template.
    /// </summary>
    public async Task UpdateWorkspaceAsync(WorkspaceTemplate workspace, CancellationToken cancellationToken = default)
    {
        var existing = _workspaces.FirstOrDefault(w => w.Id == workspace.Id);
        if (existing != null)
        {
            var index = _workspaces.IndexOf(existing);
            workspace.UpdatedAt = DateTime.UtcNow;
            _workspaces[index] = workspace;
            await SaveWorkspacesAsync(cancellationToken);

            WorkspaceUpdated?.Invoke(this, new WorkspaceEventArgs { Workspace = workspace });
        }
    }

    /// <summary>
    /// Deletes a workspace template.
    /// </summary>
    public async Task DeleteWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace != null && !workspace.IsBuiltIn)
        {
            _workspaces.Remove(workspace);
            await SaveWorkspacesAsync(cancellationToken);

            WorkspaceDeleted?.Invoke(this, new WorkspaceEventArgs { Workspace = workspace });
        }
    }

    /// <summary>
    /// Activates a workspace template.
    /// </summary>
    public async Task ActivateWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace != null)
        {
            _activeWorkspace = workspace;
            await SaveWorkspacesAsync(cancellationToken);

            WorkspaceActivated?.Invoke(this, new WorkspaceEventArgs { Workspace = workspace });
        }
    }

    /// <summary>
    /// Captures the current state as a workspace template.
    /// </summary>
    public async Task<WorkspaceTemplate> CaptureCurrentStateAsync(string name, string description, CancellationToken cancellationToken = default)
    {
        var workspace = new WorkspaceTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Category = WorkspaceCategory.Custom,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Pages = _lastSession?.OpenPages ?? new List<WorkspacePage>(),
            WidgetLayout = _lastSession?.WidgetLayout ?? new Dictionary<string, WidgetPosition>(),
            Filters = _lastSession?.ActiveFilters ?? new Dictionary<string, string>(),
            WindowBounds = _lastSession?.WindowBounds
        };

        _workspaces.Add(workspace);
        await SaveWorkspacesAsync(cancellationToken);

        return workspace;
    }

    /// <summary>
    /// Saves the current session state for restore on next launch.
    /// </summary>
    public async Task SaveSessionStateAsync(SessionState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _lastSession = state;
            state.SavedAt = DateTime.UtcNow;

            var localSettings = ApplicationData.Current.LocalSettings;
            var json = JsonSerializer.Serialize(state);
            localSettings.Values[LastSessionKey] = json;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error saving session state", ex);
        }
    }

    /// <summary>
    /// Restores the last session state.
    /// </summary>
    public SessionState? GetLastSessionState()
    {
        return _lastSession;
    }

    /// <summary>
    /// Exports a workspace to a file.
    /// </summary>
    public Task<string> ExportWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = _workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace != null)
        {
            return Task.FromResult(JsonSerializer.Serialize(workspace, new JsonSerializerOptions { WriteIndented = true }));
        }
        return Task.FromResult(string.Empty);
    }

    /// <summary>
    /// Imports a workspace from JSON.
    /// </summary>
    public async Task<WorkspaceTemplate?> ImportWorkspaceAsync(string json, CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = JsonSerializer.Deserialize<WorkspaceTemplate>(json);
            if (workspace != null)
            {
                workspace.Id = Guid.NewGuid().ToString(); // Generate new ID
                workspace.IsBuiltIn = false;
                workspace.CreatedAt = DateTime.UtcNow;
                workspace.UpdatedAt = DateTime.UtcNow;

                _workspaces.Add(workspace);
                await SaveWorkspacesAsync(cancellationToken);

                return workspace;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error importing workspace", ex);
        }
        return null;
    }

    /// <summary>
    /// Gets the default built-in workspaces.
    /// </summary>
    private static List<WorkspaceTemplate> GetDefaultWorkspaces()
    {
        return new List<WorkspaceTemplate>
        {
            new WorkspaceTemplate
            {
                Id = "monitoring",
                Name = "Monitoring",
                Description = "Real-time monitoring and data quality overview",
                Category = WorkspaceCategory.Monitoring,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Pages = new List<WorkspacePage>
                {
                    new WorkspacePage { PageTag = "Dashboard", Title = "Dashboard", IsDefault = true },
                    new WorkspacePage { PageTag = "DataQuality", Title = "Data Quality" },
                    new WorkspacePage { PageTag = "SystemHealth", Title = "System Health" },
                    new WorkspacePage { PageTag = "LiveData", Title = "Live Data" }
                }
            },
            new WorkspaceTemplate
            {
                Id = "backfill-ops",
                Name = "Backfill Operations",
                Description = "Historical data backfill and gap filling",
                Category = WorkspaceCategory.Backfill,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Pages = new List<WorkspacePage>
                {
                    new WorkspacePage { PageTag = "Backfill", Title = "Backfill", IsDefault = true },
                    new WorkspacePage { PageTag = "DataCalendar", Title = "Data Calendar" },
                    new WorkspacePage { PageTag = "ArchiveHealth", Title = "Archive Health" },
                    new WorkspacePage { PageTag = "Schedules", Title = "Schedules" }
                }
            },
            new WorkspaceTemplate
            {
                Id = "storage-admin",
                Name = "Storage Admin",
                Description = "Storage management and maintenance",
                Category = WorkspaceCategory.Storage,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Pages = new List<WorkspacePage>
                {
                    new WorkspacePage { PageTag = "Storage", Title = "Storage", IsDefault = true },
                    new WorkspacePage { PageTag = "AdminMaintenance", Title = "Maintenance" },
                    new WorkspacePage { PageTag = "PackageManager", Title = "Packages" },
                    new WorkspacePage { PageTag = "DataBrowser", Title = "Data Browser" }
                }
            },
            new WorkspaceTemplate
            {
                Id = "analysis-export",
                Name = "Analysis & Export",
                Description = "Data analysis and export workflows",
                Category = WorkspaceCategory.Analysis,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Pages = new List<WorkspacePage>
                {
                    new WorkspacePage { PageTag = "AdvancedAnalytics", Title = "Analytics", IsDefault = true },
                    new WorkspacePage { PageTag = "AnalysisExportWizard", Title = "Export Wizard" },
                    new WorkspacePage { PageTag = "DataExport", Title = "Data Export" },
                    new WorkspacePage { PageTag = "LeanIntegration", Title = "Lean Integration" }
                }
            }
        };
    }

    /// <summary>
    /// Event raised when a workspace is created.
    /// </summary>
    public event EventHandler<WorkspaceEventArgs>? WorkspaceCreated;

    /// <summary>
    /// Event raised when a workspace is updated.
    /// </summary>
    public event EventHandler<WorkspaceEventArgs>? WorkspaceUpdated;

    /// <summary>
    /// Event raised when a workspace is deleted.
    /// </summary>
    public event EventHandler<WorkspaceEventArgs>? WorkspaceDeleted;

    /// <summary>
    /// Event raised when a workspace is activated.
    /// </summary>
    public event EventHandler<WorkspaceEventArgs>? WorkspaceActivated;
}
