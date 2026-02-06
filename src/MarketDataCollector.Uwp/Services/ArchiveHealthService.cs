using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for monitoring archive health and verification (#26/57 - P0 Critical).
/// Provides comprehensive archive integrity monitoring and verification.
/// </summary>
public sealed class ArchiveHealthService
{
    private static ArchiveHealthService? _instance;
    private static readonly object _lock = new();

    private readonly ConfigService _configService;
    private readonly NotificationService _notificationService;
    private readonly string _healthStatusPath;
    private ArchiveHealthStatus? _cachedHealthStatus;
    private DateTime _lastHealthCheck;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private VerificationJob? _currentVerificationJob;
    private CancellationTokenSource? _verificationCts;

    public static ArchiveHealthService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ArchiveHealthService();
                }
            }
            return _instance;
        }
    }

    private ArchiveHealthService()
    {
        _configService = new ConfigService();
        _notificationService = NotificationService.Instance;
        _healthStatusPath = Path.Combine(AppContext.BaseDirectory, "_catalog", "archive_health.json");
    }

    /// <summary>
    /// Gets the current archive health status.
    /// </summary>
    public async Task<ArchiveHealthStatus> GetHealthStatusAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedHealthStatus != null &&
            DateTime.UtcNow - _lastHealthCheck < _cacheExpiration)
        {
            return _cachedHealthStatus;
        }

        var status = await CalculateHealthStatusAsync();
        _cachedHealthStatus = status;
        _lastHealthCheck = DateTime.UtcNow;

        // Save status
        await SaveHealthStatusAsync(status);

        HealthStatusUpdated?.Invoke(this, new ArchiveHealthEventArgs { Status = status });

        return status;
    }

    /// <summary>
    /// Starts a full archive verification job.
    /// </summary>
    public async Task<VerificationJob> StartFullVerificationAsync(IProgress<VerificationProgress>? progress = null)
    {
        if (_currentVerificationJob?.Status == "Running")
        {
            throw new InvalidOperationException("A verification job is already running.");
        }

        _verificationCts = new CancellationTokenSource();
        _currentVerificationJob = new VerificationJob
        {
            Type = "Full",
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        VerificationStarted?.Invoke(this, new VerificationJobEventArgs { Job = _currentVerificationJob });

        try
        {
            await RunVerificationAsync(_currentVerificationJob, null, progress, _verificationCts.Token);

            _currentVerificationJob.Status = _currentVerificationJob.FailedFiles > 0 ? "Failed" : "Completed";
            _currentVerificationJob.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            _currentVerificationJob.Status = "Cancelled";
            _currentVerificationJob.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _currentVerificationJob.Status = "Failed";
            _currentVerificationJob.CompletedAt = DateTime.UtcNow;
            _currentVerificationJob.Errors = (_currentVerificationJob.Errors?.ToList() ?? new List<string>())
                .Append(ex.Message).ToArray();
        }

        VerificationCompleted?.Invoke(this, new VerificationJobEventArgs { Job = _currentVerificationJob });

        // Refresh health status
        await GetHealthStatusAsync(true);

        return _currentVerificationJob;
    }

    /// <summary>
    /// Starts an incremental verification (only new/modified files).
    /// </summary>
    public async Task<VerificationJob> StartIncrementalVerificationAsync(DateTime since, IProgress<VerificationProgress>? progress = null)
    {
        if (_currentVerificationJob?.Status == "Running")
        {
            throw new InvalidOperationException("A verification job is already running.");
        }

        _verificationCts = new CancellationTokenSource();
        _currentVerificationJob = new VerificationJob
        {
            Type = "Incremental",
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        VerificationStarted?.Invoke(this, new VerificationJobEventArgs { Job = _currentVerificationJob });

        try
        {
            await RunVerificationAsync(_currentVerificationJob, since, progress, _verificationCts.Token);

            _currentVerificationJob.Status = _currentVerificationJob.FailedFiles > 0 ? "Failed" : "Completed";
            _currentVerificationJob.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            _currentVerificationJob.Status = "Cancelled";
            _currentVerificationJob.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _currentVerificationJob.Status = "Failed";
            _currentVerificationJob.CompletedAt = DateTime.UtcNow;
            _currentVerificationJob.Errors = (_currentVerificationJob.Errors?.ToList() ?? new List<string>())
                .Append(ex.Message).ToArray();
        }

        VerificationCompleted?.Invoke(this, new VerificationJobEventArgs { Job = _currentVerificationJob });

        return _currentVerificationJob;
    }

    /// <summary>
    /// Cancels the current verification job.
    /// </summary>
    public void CancelVerification()
    {
        _verificationCts?.Cancel();
    }

    /// <summary>
    /// Gets the current verification job status.
    /// </summary>
    public VerificationJob? GetCurrentVerificationJob()
    {
        return _currentVerificationJob;
    }

    /// <summary>
    /// Attempts to repair a failed file by re-validating or restoring from backup.
    /// </summary>
    public async Task<bool> TryRepairFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            // Try to verify the file is readable and valid
            var fileInfo = new FileInfo(filePath);

            if (filePath.EndsWith(".jsonl") || filePath.EndsWith(".jsonl.gz"))
            {
                // Try to read and validate JSONL
                using var stream = filePath.EndsWith(".gz")
                    ? new System.IO.Compression.GZipStream(File.OpenRead(filePath), System.IO.Compression.CompressionMode.Decompress)
                    : File.OpenRead(filePath) as Stream;

                using var reader = new StreamReader(stream);
                var lineCount = 0;
                while (!reader.EndOfStream && lineCount < 10)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        // Try to parse as JSON
                        JsonDocument.Parse(line);
                    }
                    lineCount++;
                }

                return true;
            }

            return fileInfo.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets issues affecting a specific symbol.
    /// </summary>
    public async Task<ArchiveIssue[]> GetIssuesForSymbolAsync(string symbol)
    {
        var status = await GetHealthStatusAsync();
        return status.Issues?.Where(i =>
            i.AffectedSymbols?.Contains(symbol, StringComparer.OrdinalIgnoreCase) == true
        ).ToArray() ?? Array.Empty<ArchiveIssue>();
    }

    /// <summary>
    /// Acknowledges/resolves an issue.
    /// </summary>
    public async Task ResolveIssueAsync(string issueId)
    {
        var status = await GetHealthStatusAsync();
        var issue = status.Issues?.FirstOrDefault(i => i.Id == issueId);

        if (issue != null)
        {
            issue.ResolvedAt = DateTime.UtcNow;
            await SaveHealthStatusAsync(status);
            IssueResolved?.Invoke(this, new ArchiveIssueEventArgs { Issue = issue });
        }
    }

    private async Task<ArchiveHealthStatus> CalculateHealthStatusAsync()
    {
        var config = await _configService.LoadConfigAsync();
        var dataRoot = config?.DataRoot ?? "data";
        var basePath = Path.IsPathRooted(dataRoot)
            ? dataRoot
            : Path.Combine(AppContext.BaseDirectory, dataRoot);

        var status = new ArchiveHealthStatus
        {
            LastUpdated = DateTime.UtcNow
        };

        var issues = new List<ArchiveIssue>();

        // Get storage health info
        status.StorageHealthInfo = await GetStorageHealthInfoAsync(basePath);

        // Check storage capacity
        if (status.StorageHealthInfo.UsedPercent >= 95)
        {
            issues.Add(new ArchiveIssue
            {
                Severity = "Critical",
                Category = "Storage",
                Message = $"Storage is {status.StorageHealthInfo.UsedPercent:F1}% full. Immediate action required.",
                SuggestedAction = "Free up disk space or move data to cold storage",
                IsAutoFixable = false
            });
        }
        else if (status.StorageHealthInfo.UsedPercent >= 85)
        {
            issues.Add(new ArchiveIssue
            {
                Severity = "Warning",
                Category = "Storage",
                Message = $"Storage is {status.StorageHealthInfo.UsedPercent:F1}% full.",
                SuggestedAction = "Consider archiving older data to cold storage",
                IsAutoFixable = false
            });
        }

        // Scan files
        if (Directory.Exists(basePath))
        {
            await Task.Run(() =>
            {
                var files = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".jsonl") || f.EndsWith(".jsonl.gz") || f.EndsWith(".parquet"))
                    .ToList();

                status.TotalFiles = files.Count;
                status.TotalSizeBytes = files.Sum(f => new FileInfo(f).Length);

                // Check for empty files
                var emptyFiles = files.Where(f => new FileInfo(f).Length == 0).ToList();
                if (emptyFiles.Any())
                {
                    issues.Add(new ArchiveIssue
                    {
                        Severity = "Warning",
                        Category = "Integrity",
                        Message = $"Found {emptyFiles.Count} empty data files",
                        AffectedFiles = emptyFiles.Take(10).ToArray(),
                        SuggestedAction = "Delete or re-download affected files",
                        IsAutoFixable = true
                    });
                }

                // Check for very old unverified files
                var catalogPath = Path.Combine(AppContext.BaseDirectory, "_catalog");
                if (Directory.Exists(catalogPath))
                {
                    var manifests = Directory.GetFiles(catalogPath, "*.json")
                        .Where(f => !f.Contains("archive_health") && !f.Contains("data_dictionary"));

                    // Count files mentioned in manifests as verified
                    foreach (var manifestFile in manifests)
                    {
                        try
                        {
                            var json = File.ReadAllText(manifestFile);
                            var manifest = JsonSerializer.Deserialize<DataManifest>(json, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (manifest?.Files != null)
                            {
                                var verifiedInManifest = manifest.Files.Count(f => f.VerificationStatus == "Verified");
                                status.VerifiedFiles += verifiedInManifest;
                            }
                        }
                        catch
                        {
                            // Skip invalid manifests
                        }
                    }
                }

                status.PendingFiles = status.TotalFiles - status.VerifiedFiles - status.FailedFiles;
            });
        }

        status.Issues = issues.ToArray();

        // Calculate recommendations
        var recommendations = new List<string>();
        if (status.PendingFiles > status.TotalFiles * 0.2)
        {
            recommendations.Add("Run a full verification to ensure archive integrity");
        }
        if (status.StorageHealthInfo.DaysUntilFull.HasValue && status.StorageHealthInfo.DaysUntilFull < 30)
        {
            recommendations.Add($"Storage will be full in ~{status.StorageHealthInfo.DaysUntilFull} days. Plan for capacity expansion.");
        }
        if (status.FailedFiles > 0)
        {
            recommendations.Add($"Repair or re-download {status.FailedFiles} failed files");
        }

        status.Recommendations = recommendations.ToArray();

        // Calculate overall health score
        status.OverallHealthScore = CalculateOverallHealthScore(status);
        status.Status = status.OverallHealthScore switch
        {
            >= 90 => "Healthy",
            >= 70 => "Warning",
            _ => "Critical"
        };

        return status;
    }

    private async Task<StorageHealthInfo> GetStorageHealthInfoAsync(string basePath)
    {
        var info = new StorageHealthInfo();

        await Task.Run(() =>
        {
            try
            {
                var root = Path.GetPathRoot(basePath);
                if (string.IsNullOrEmpty(root))
                {
                    root = Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\";
                }

                var driveInfo = new DriveInfo(root);
                info.TotalCapacity = driveInfo.TotalSize;
                info.FreeSpace = driveInfo.AvailableFreeSpace;
                info.UsedPercent = (1.0 - (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize) * 100;
                info.DriveType = driveInfo.DriveType.ToString();

                // Estimate health status based on drive type
                info.HealthStatus = driveInfo.DriveType switch
                {
                    DriveType.Fixed or DriveType.Removable => "Good",
                    DriveType.Network => "Unknown",
                    _ => "Unknown"
                };

                // Estimate days until full based on recent growth
                var analyticsService = StorageAnalyticsService.Instance;
                var analytics = analyticsService.GetAnalyticsAsync(false).GetAwaiter().GetResult();
                if (analytics.DailyGrowthBytes > 0)
                {
                    info.DaysUntilFull = (int)(driveInfo.AvailableFreeSpace * 0.9 / analytics.DailyGrowthBytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get drive info: {ex.Message}");
            }
        });

        return info;
    }

    private async Task RunVerificationAsync(VerificationJob job, DateTime? since,
        IProgress<VerificationProgress>? progress, CancellationToken cancellationToken)
    {
        var config = await _configService.LoadConfigAsync();
        var dataRoot = config?.DataRoot ?? "data";
        var basePath = Path.IsPathRooted(dataRoot)
            ? dataRoot
            : Path.Combine(AppContext.BaseDirectory, dataRoot);

        if (!Directory.Exists(basePath))
        {
            return;
        }

        var files = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".jsonl") || f.EndsWith(".jsonl.gz") || f.EndsWith(".parquet"))
            .ToList();

        // Filter by date if incremental
        if (since.HasValue)
        {
            files = files.Where(f => new FileInfo(f).LastWriteTimeUtc >= since.Value).ToList();
        }

        job.TotalFiles = files.Count;
        var errors = new List<string>();
        var startTime = DateTime.UtcNow;

        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = files[i];
            job.ProcessedFiles = i + 1;

            try
            {
                var isValid = await VerifyFileAsync(file);
                if (isValid)
                {
                    job.VerifiedFiles++;
                }
                else
                {
                    job.FailedFiles++;
                    errors.Add($"Verification failed: {file}");
                }
            }
            catch (Exception ex)
            {
                job.FailedFiles++;
                errors.Add($"{file}: {ex.Message}");
            }

            // Calculate progress
            var elapsed = DateTime.UtcNow - startTime;
            job.ProgressPercent = (double)(i + 1) / files.Count * 100;
            job.FilesPerSecond = (i + 1) / elapsed.TotalSeconds;

            if (i + 1 < files.Count && job.FilesPerSecond > 0)
            {
                job.EstimatedTimeRemainingSeconds = (int)((files.Count - i - 1) / job.FilesPerSecond);
            }

            progress?.Report(new VerificationProgress
            {
                ProcessedFiles = job.ProcessedFiles,
                TotalFiles = job.TotalFiles,
                VerifiedFiles = job.VerifiedFiles,
                FailedFiles = job.FailedFiles,
                ProgressPercent = job.ProgressPercent,
                CurrentFile = Path.GetFileName(file),
                FilesPerSecond = job.FilesPerSecond,
                EstimatedTimeRemainingSeconds = job.EstimatedTimeRemainingSeconds
            });

            // Yield occasionally to keep UI responsive
            if (i % 10 == 0)
            {
                await Task.Delay(1, cancellationToken);
            }
        }

        job.Errors = errors.ToArray();
    }

    private async Task<bool> VerifyFileAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                // Check file exists and has content
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    return false;
                }

                // Try to compute checksum (validates file is readable)
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);

                // For compressed files, verify decompression
                if (filePath.EndsWith(".gz"))
                {
                    using var gzipStream = new System.IO.Compression.GZipStream(stream,
                        System.IO.Compression.CompressionMode.Decompress);

                    // Read a small buffer to verify decompression works
                    var buffer = new byte[1024];
                    gzipStream.Read(buffer, 0, buffer.Length);
                }
                else
                {
                    // Compute checksum for uncompressed files
                    sha256.ComputeHash(stream);
                }

                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    private static double CalculateOverallHealthScore(ArchiveHealthStatus status)
    {
        var score = 100.0;

        // Deduct for failed files
        if (status.TotalFiles > 0)
        {
            var failedPercent = (double)status.FailedFiles / status.TotalFiles * 100;
            score -= failedPercent * 2;
        }

        // Deduct for storage issues
        if (status.StorageHealthInfo?.UsedPercent >= 95)
        {
            score -= 30;
        }
        else if (status.StorageHealthInfo?.UsedPercent >= 85)
        {
            score -= 10;
        }

        // Deduct for critical issues
        var criticalIssues = status.Issues?.Count(i => i.Severity == "Critical") ?? 0;
        score -= criticalIssues * 15;

        // Deduct for warning issues
        var warningIssues = status.Issues?.Count(i => i.Severity == "Warning") ?? 0;
        score -= warningIssues * 5;

        // Deduct for many unverified files
        if (status.TotalFiles > 0 && status.PendingFiles > status.TotalFiles * 0.5)
        {
            score -= 10;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private async Task SaveHealthStatusAsync(ArchiveHealthStatus status)
    {
        try
        {
            var directory = Path.GetDirectoryName(_healthStatusPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(_healthStatusPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save health status: {ex.Message}");
        }
    }

    // Events
    public event EventHandler<ArchiveHealthEventArgs>? HealthStatusUpdated;
    public event EventHandler<VerificationJobEventArgs>? VerificationStarted;
    public event EventHandler<VerificationJobEventArgs>? VerificationCompleted;
    public event EventHandler<ArchiveIssueEventArgs>? IssueResolved;
}

/// <summary>
/// Event args for archive health events.
/// </summary>
public class ArchiveHealthEventArgs : EventArgs
{
    public ArchiveHealthStatus? Status { get; set; }
}

/// <summary>
/// Event args for verification job events.
/// </summary>
public class VerificationJobEventArgs : EventArgs
{
    public VerificationJob? Job { get; set; }
}

/// <summary>
/// Event args for issue resolution.
/// </summary>
public class ArchiveIssueEventArgs : EventArgs
{
    public ArchiveIssue? Issue { get; set; }
}

/// <summary>
/// Progress information for verification.
/// </summary>
public class VerificationProgress
{
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public int VerifiedFiles { get; set; }
    public int FailedFiles { get; set; }
    public double ProgressPercent { get; set; }
    public string? CurrentFile { get; set; }
    public double FilesPerSecond { get; set; }
    public int? EstimatedTimeRemainingSeconds { get; set; }
}
