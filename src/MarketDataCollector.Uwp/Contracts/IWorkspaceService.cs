using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for managing workspace templates and session restore.
/// </summary>
public interface IWorkspaceService
{
    WorkspaceTemplate? ActiveWorkspace { get; }
    SessionState? LastSession { get; }
    IReadOnlyList<WorkspaceTemplate> Workspaces { get; }

    Task LoadWorkspacesAsync(CancellationToken cancellationToken = default);
    Task SaveWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceTemplate> CreateWorkspaceAsync(string name, string description, WorkspaceCategory category, CancellationToken cancellationToken = default);
    Task UpdateWorkspaceAsync(WorkspaceTemplate workspace, CancellationToken cancellationToken = default);
    Task DeleteWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task ActivateWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceTemplate> CaptureCurrentStateAsync(string name, string description, CancellationToken cancellationToken = default);
    Task SaveSessionStateAsync(SessionState state, CancellationToken cancellationToken = default);
    SessionState? GetLastSessionState();
    Task<string> ExportWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceTemplate?> ImportWorkspaceAsync(string json, CancellationToken cancellationToken = default);

    event EventHandler<WorkspaceEventArgs>? WorkspaceCreated;
    event EventHandler<WorkspaceEventArgs>? WorkspaceUpdated;
    event EventHandler<WorkspaceEventArgs>? WorkspaceDeleted;
    event EventHandler<WorkspaceEventArgs>? WorkspaceActivated;
}
