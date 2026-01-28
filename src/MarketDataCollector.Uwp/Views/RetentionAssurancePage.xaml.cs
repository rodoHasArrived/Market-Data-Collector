using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Retention Assurance page for managing retention policies with guardrails and legal holds.
/// Implements Feature Refinement #23 - File Retention Assurance.
/// </summary>
public sealed partial class RetentionAssurancePage : Page
{
    private readonly RetentionAssuranceService _retentionService;
    private readonly NotificationService _notificationService;
    private readonly ConfigService _configService;
    private RetentionDryRunResult? _lastDryRunResult;
    private CancellationTokenSource? _operationCts;

    public RetentionAssurancePage()
    {
        this.InitializeComponent();
        _retentionService = RetentionAssuranceService.Instance;
        _notificationService = NotificationService.Instance;
        _configService = ConfigService.Instance;

        // Subscribe to events
        _retentionService.LegalHoldCreated += RetentionService_LegalHoldChanged;
        _retentionService.LegalHoldReleased += RetentionService_LegalHoldChanged;

        Loaded += RetentionAssurancePage_Loaded;
        Unloaded += RetentionAssurancePage_Unloaded;
    }

    private void RetentionAssurancePage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = LoadDataAsync();
    }

    private void RetentionAssurancePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _retentionService.LegalHoldCreated -= RetentionService_LegalHoldChanged;
        _retentionService.LegalHoldReleased -= RetentionService_LegalHoldChanged;
        _operationCts?.Cancel();
    }

    private void RetentionService_LegalHoldChanged(object? sender, LegalHoldEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => LoadLegalHolds());
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await _retentionService.LoadConfigurationAsync();

            // Load guardrails into UI
            LoadGuardrailsUI();

            // Load legal holds
            LoadLegalHolds();

            // Load audit reports
            LoadAuditReports();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RetentionAssurancePage] Error loading data: {ex.Message}");
        }
    }

    private void LoadGuardrailsUI()
    {
        var guardrails = _retentionService.Configuration.Guardrails;

        MinTickDataDays.Value = guardrails.MinTickDataDays;
        MinBarDataDays.Value = guardrails.MinBarDataDays;
        MinQuoteDataDays.Value = guardrails.MinQuoteDataDays;
        MaxDailyDeletedFiles.Value = guardrails.MaxDailyDeletedFiles;
        RequireChecksumCheck.IsChecked = guardrails.RequireChecksumVerification;
        RequireDryRunCheck.IsChecked = guardrails.RequireDryRunPreview;
    }

    private void LoadLegalHolds()
    {
        var holds = _retentionService.LegalHolds.Where(h => h.IsActive).ToList();
        LegalHoldsList.ItemsSource = holds;
        LegalHoldCount.Text = $"({holds.Count} active)";
        NoLegalHoldsText.Visibility = holds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LegalHoldsList.Visibility = holds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadAuditReports()
    {
        var reports = _retentionService.AuditReports.ToList();
        AuditReportsList.ItemsSource = reports;
        NoAuditReportsText.Visibility = reports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AuditReportsList.Visibility = reports.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #region Event Handlers

    private async void SaveGuardrails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = _retentionService.Configuration;
            config.Guardrails.MinTickDataDays = (int)MinTickDataDays.Value;
            config.Guardrails.MinBarDataDays = (int)MinBarDataDays.Value;
            config.Guardrails.MinQuoteDataDays = (int)MinQuoteDataDays.Value;
            config.Guardrails.MaxDailyDeletedFiles = (int)MaxDailyDeletedFiles.Value;
            config.Guardrails.RequireChecksumVerification = RequireChecksumCheck.IsChecked ?? true;
            config.Guardrails.RequireDryRunPreview = RequireDryRunCheck.IsChecked ?? true;

            await _retentionService.SaveConfigurationAsync();
            await _notificationService.NotifyAsync("Guardrails Saved", "Retention guardrails have been updated", NotificationType.Success);
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Save Failed", ex.Message);
        }
    }

    private async void CreateLegalHold_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Create Legal Hold",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var panel = new StackPanel { Spacing = 16 };
        var nameBox = new TextBox { Header = "Hold Name", PlaceholderText = "e.g., Q4 Audit Investigation" };
        var reasonBox = new TextBox
        {
            Header = "Reason",
            PlaceholderText = "Reason for the legal hold",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };
        var symbolsBox = new TextBox
        {
            Header = "Symbols (comma-separated)",
            PlaceholderText = "e.g., AAPL, MSFT, GOOGL or * for all"
        };
        var expiresCheck = new CheckBox { Content = "Set expiration date" };
        var expiresDate = new CalendarDatePicker { Header = "Expires On", IsEnabled = false };
        expiresCheck.Checked += (s, args) => expiresDate.IsEnabled = true;
        expiresCheck.Unchecked += (s, args) => expiresDate.IsEnabled = false;

        panel.Children.Add(nameBox);
        panel.Children.Add(reasonBox);
        panel.Children.Add(symbolsBox);
        panel.Children.Add(expiresCheck);
        panel.Children.Add(expiresDate);
        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            var symbols = symbolsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            DateTime? expiresAt = null;
            if (expiresCheck.IsChecked == true && expiresDate.Date.HasValue)
            {
                expiresAt = expiresDate.Date.Value.DateTime;
            }

            await _retentionService.CreateLegalHoldAsync(nameBox.Text, reasonBox.Text, symbols, expiresAt);
            await _notificationService.NotifyAsync("Legal Hold Created", $"Created legal hold '{nameBox.Text}'", NotificationType.Success);
        }
    }

    private async void ViewHoldSymbols_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string holdId)
        {
            var hold = _retentionService.LegalHolds.FirstOrDefault(h => h.Id == holdId);
            if (hold != null)
            {
                var dialog = new ContentDialog
                {
                    Title = $"Symbols Under Hold: {hold.Name}",
                    CloseButtonText = "Close",
                    XamlRoot = this.XamlRoot,
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = string.Join(", ", hold.Symbols),
                            TextWrapping = TextWrapping.Wrap
                        },
                        MaxHeight = 300
                    }
                };
                await dialog.ShowAsync();
            }
        }
    }

    private async void ReleaseLegalHold_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string holdId)
        {
            var hold = _retentionService.LegalHolds.FirstOrDefault(h => h.Id == holdId);
            if (hold != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Release Legal Hold",
                    Content = $"Are you sure you want to release '{hold.Name}'? This will allow cleanup of held symbols.",
                    PrimaryButtonText = "Release",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await _retentionService.ReleaseLegalHoldAsync(holdId);
                    await _notificationService.NotifyAsync("Legal Hold Released", $"Released hold '{hold.Name}'", NotificationType.Info);
                }
            }
        }
    }

    private async void RunDryRun_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get policy from UI
            var policy = new RetentionPolicy
            {
                TickDataDays = (int)TickDataDays.Value,
                BarDataDays = (int)BarDataDays.Value,
                QuoteDataDays = (int)QuoteDataDays.Value,
                DepthDataDays = (int)DepthDataDays.Value,
                CompressBeforeDelete = CompressBeforeDeleteCheck.IsChecked ?? true,
                ArchiveToCloud = ArchiveToCloudCheck.IsChecked ?? false
            };

            // Validate policy
            var validation = _retentionService.ValidateRetentionPolicy(policy);
            if (!validation.IsValid)
            {
                await ShowValidationErrorsAsync(validation);
                return;
            }

            // Show warnings if any
            if (validation.Warnings.Any())
            {
                ValidationWarningsSection.Visibility = Visibility.Visible;
                ValidationWarningsList.ItemsSource = validation.Warnings.Select(w => w.Message);
            }
            else
            {
                ValidationWarningsSection.Visibility = Visibility.Collapsed;
            }

            // Get data root from config
            var config = await _configService.LoadConfigAsync();
            var dataRoot = config?.DataRoot ?? "data";

            // Run dry run
            _operationCts = new CancellationTokenSource();
            _lastDryRunResult = await _retentionService.PerformDryRunAsync(policy, dataRoot, _operationCts.Token);

            // Display results
            DisplayDryRunResults(_lastDryRunResult);
        }
        catch (OperationCanceledException)
        {
            await _notificationService.NotifyAsync("Dry Run Cancelled", "Operation was cancelled", NotificationType.Info);
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Dry Run Failed", ex.Message);
        }
    }

    private async Task ShowValidationErrorsAsync(RetentionValidationResult validation)
    {
        var errors = string.Join("\n", validation.Violations.Select(v => $"• {v.Message}"));
        var dialog = new ContentDialog
        {
            Title = "Validation Failed",
            Content = new TextBlock { Text = errors, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void DisplayDryRunResults(RetentionDryRunResult result)
    {
        DryRunResultsSection.Visibility = Visibility.Visible;

        FilesToDeleteCount.Text = result.FilesToDelete.Count.ToString("N0");
        BytesToDeleteText.Text = FormatBytes(result.TotalBytesToDelete);
        SymbolsAffectedCount.Text = result.BySymbol.Count.ToString("N0");
        SkippedFilesCount.Text = result.SkippedFiles.Count.ToString("N0");

        // Populate by-symbol list
        var bySymbolList = result.BySymbol.Values.Select(s => new
        {
            s.Symbol,
            s.FileCount,
            SizeFormatted = FormatBytes(s.TotalBytes),
            DataTypesText = string.Join(", ", s.DataTypes)
        }).ToList();
        BySymbolList.ItemsSource = bySymbolList;

        // Populate skipped files list
        SkippedFilesList.ItemsSource = result.SkippedFiles;

        // Enable execute button if there are files to delete
        ExecuteCleanupButton.IsEnabled = result.FilesToDelete.Any();
    }

    private async void ExecuteCleanup_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDryRunResult == null || !_lastDryRunResult.FilesToDelete.Any())
        {
            return;
        }

        // Confirm execution
        var dialog = new ContentDialog
        {
            Title = "Confirm Cleanup Execution",
            Content = $"This will permanently delete {_lastDryRunResult.FilesToDelete.Count:N0} files ({FormatBytes(_lastDryRunResult.TotalBytesToDelete)}).\n\nThis action cannot be undone.",
            PrimaryButtonText = "Execute",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _operationCts = new CancellationTokenSource();
            var verifyChecksums = RequireChecksumCheck.IsChecked ?? true;

            var report = await _retentionService.ExecuteRetentionCleanupAsync(
                _lastDryRunResult,
                verifyChecksums,
                _operationCts.Token);

            // Refresh audit reports
            LoadAuditReports();

            // Show result
            var statusMessage = report.Status switch
            {
                CleanupStatus.Success => $"Successfully deleted {report.DeletedFiles.Count:N0} files, freed {FormatBytes(report.ActualBytesDeleted)}",
                CleanupStatus.PartialSuccess => $"Partially completed: {report.DeletedFiles.Count:N0} files deleted, {report.Errors.Count} errors",
                CleanupStatus.FailedVerification => "Cleanup aborted: Checksum verification failed",
                _ => "Cleanup failed"
            };

            var notificationType = report.Status == CleanupStatus.Success
                ? NotificationType.Success
                : report.Status == CleanupStatus.PartialSuccess
                    ? NotificationType.Warning
                    : NotificationType.Error;

            await _notificationService.NotifyAsync("Cleanup Complete", statusMessage, notificationType);

            // Clear dry run results
            _lastDryRunResult = null;
            DryRunResultsSection.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Cleanup Failed", ex.Message);
        }
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDryRunResult == null)
        {
            await _notificationService.NotifyWarningAsync("No Results", "Run a dry run first to generate a report");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            picker.SuggestedFileName = $"retention_dryrun_{DateTime.Now:yyyyMMdd_HHmmss}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_lastDryRunResult,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(file, json);
                await _notificationService.NotifyAsync("Export Complete", $"Exported to {file.Name}", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
        }
    }

    private void ViewAuditReport_Click(object sender, RoutedEventArgs e)
    {
        // Show audit report details in a dialog
    }

    private async void ExportAuditReport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string reportId)
        {
            var report = _retentionService.AuditReports.FirstOrDefault(r => r.Id == reportId);
            if (report != null)
            {
                try
                {
                    var json = await _retentionService.ExportAuditReportAsync(report);

                    var picker = new FileSavePicker();
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
                    picker.SuggestedFileName = $"retention_audit_{report.ExecutedAt:yyyyMMdd_HHmmss}";

                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        await FileIO.WriteTextAsync(file, json);
                        await _notificationService.NotifyAsync("Export Complete", $"Exported to {file.Name}", NotificationType.Success);
                    }
                }
                catch (Exception ex)
                {
                    await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
                }
            }
        }
    }

    private async void GenerateAttestation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Generate a compliance attestation report
            var attestation = await GenerateComplianceAttestationAsync();

            // Show dialog to select output format
            var dialog = new ContentDialog
            {
                Title = "Generate Attestation Report",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "This will generate a compliance attestation report documenting:" },
                        new TextBlock { Text = "• Current retention policy configuration", Margin = new Thickness(16, 0, 0, 0) },
                        new TextBlock { Text = "• Active legal holds", Margin = new Thickness(16, 0, 0, 0) },
                        new TextBlock { Text = "• Guardrail settings", Margin = new Thickness(16, 0, 0, 0) },
                        new TextBlock { Text = "• Recent audit history", Margin = new Thickness(16, 0, 0, 0) },
                        new TextBlock { Text = "• Data integrity verification status", Margin = new Thickness(16, 0, 0, 0) }
                    }
                },
                PrimaryButtonText = "Generate Report",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            // Save the report
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.FileTypeChoices.Add("Text", new List<string> { ".txt" });
            picker.SuggestedFileName = $"compliance_attestation_{DateTime.Now:yyyyMMdd_HHmmss}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                string content;
                if (file.FileType == ".json")
                {
                    content = System.Text.Json.JsonSerializer.Serialize(attestation,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    content = GenerateTextReport(attestation);
                }

                await FileIO.WriteTextAsync(file, content);
                await _notificationService.NotifyAsync("Attestation Generated", $"Report saved to {file.Name}", NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Generation Failed", ex.Message);
        }
    }

    private async Task<ComplianceAttestation> GenerateComplianceAttestationAsync()
    {
        var config = await _configService.LoadConfigAsync();

        return new ComplianceAttestation
        {
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = Environment.UserName,
            MachineName = Environment.MachineName,

            // Retention Configuration
            RetentionConfiguration = new RetentionConfigurationSummary
            {
                TickDataRetentionDays = _retentionService.Configuration.DefaultPolicy.TickDataDays,
                BarDataRetentionDays = _retentionService.Configuration.DefaultPolicy.BarDataDays,
                QuoteDataRetentionDays = _retentionService.Configuration.DefaultPolicy.QuoteDataDays,
                CompressBeforeDelete = _retentionService.Configuration.DefaultPolicy.CompressBeforeDelete,
                ArchiveToCloud = _retentionService.Configuration.DefaultPolicy.ArchiveToCloud
            },

            // Guardrails
            Guardrails = new GuardrailsSummary
            {
                MinTickDataDays = _retentionService.Configuration.Guardrails.MinTickDataDays,
                MinBarDataDays = _retentionService.Configuration.Guardrails.MinBarDataDays,
                MinQuoteDataDays = _retentionService.Configuration.Guardrails.MinQuoteDataDays,
                MaxDailyDeletedFiles = _retentionService.Configuration.Guardrails.MaxDailyDeletedFiles,
                RequireChecksumVerification = _retentionService.Configuration.Guardrails.RequireChecksumVerification,
                RequireDryRunPreview = _retentionService.Configuration.Guardrails.RequireDryRunPreview
            },

            // Legal Holds
            ActiveLegalHolds = _retentionService.LegalHolds
                .Where(h => h.IsActive)
                .Select(h => new LegalHoldSummary
                {
                    Name = h.Name,
                    CreatedAt = h.CreatedAt,
                    Reason = h.Reason ?? "Not specified",
                    SymbolCount = h.Symbols.Count,
                    ExpiresAt = h.ExpiresAt
                })
                .ToList(),

            // Audit History
            RecentAudits = _retentionService.AuditReports
                .OrderByDescending(a => a.ExecutedAt)
                .Take(10)
                .Select(a => new AuditSummary
                {
                    ExecutedAt = a.ExecutedAt,
                    Status = a.Status.ToString(),
                    FilesDeleted = a.DeletedFiles.Count,
                    BytesFreed = a.ActualBytesDeleted,
                    ErrorCount = a.Errors.Count
                })
                .ToList(),

            // System Status
            SystemStatus = new SystemStatusSummary
            {
                DataRootPath = config?.DataRoot ?? "data",
                TotalSymbolsConfigured = config?.Symbols?.Length ?? 0,
                ApplicationVersion = "1.6.1",
                LastConfigurationChange = _retentionService.Configuration.LastModified
            },

            // Attestation Statement
            AttestationStatement = "This attestation confirms the current retention policy configuration " +
                "and compliance controls are in place. All data retention operations are subject to " +
                "the guardrails and legal hold protections defined in this document."
        };
    }

    private static string GenerateTextReport(ComplianceAttestation attestation)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine("                    DATA RETENTION COMPLIANCE ATTESTATION                      ");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Generated: {attestation.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Generated By: {attestation.GeneratedBy}");
        sb.AppendLine($"Machine: {attestation.MachineName}");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("RETENTION CONFIGURATION");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"  Tick Data Retention:    {attestation.RetentionConfiguration.TickDataRetentionDays} days");
        sb.AppendLine($"  Bar Data Retention:     {attestation.RetentionConfiguration.BarDataRetentionDays} days");
        sb.AppendLine($"  Quote Data Retention:   {attestation.RetentionConfiguration.QuoteDataRetentionDays} days");
        sb.AppendLine($"  Compress Before Delete: {attestation.RetentionConfiguration.CompressBeforeDelete}");
        sb.AppendLine($"  Archive to Cloud:       {attestation.RetentionConfiguration.ArchiveToCloud}");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("GUARDRAILS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"  Minimum Tick Data Days:        {attestation.Guardrails.MinTickDataDays}");
        sb.AppendLine($"  Minimum Bar Data Days:         {attestation.Guardrails.MinBarDataDays}");
        sb.AppendLine($"  Minimum Quote Data Days:       {attestation.Guardrails.MinQuoteDataDays}");
        sb.AppendLine($"  Max Daily Deleted Files:       {attestation.Guardrails.MaxDailyDeletedFiles}");
        sb.AppendLine($"  Require Checksum Verification: {attestation.Guardrails.RequireChecksumVerification}");
        sb.AppendLine($"  Require Dry Run Preview:       {attestation.Guardrails.RequireDryRunPreview}");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"ACTIVE LEGAL HOLDS ({attestation.ActiveLegalHolds.Count})");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (attestation.ActiveLegalHolds.Any())
        {
            foreach (var hold in attestation.ActiveLegalHolds)
            {
                sb.AppendLine($"  * {hold.Name}");
                sb.AppendLine($"      Created: {hold.CreatedAt:yyyy-MM-dd}");
                sb.AppendLine($"      Reason: {hold.Reason}");
                sb.AppendLine($"      Symbols Protected: {hold.SymbolCount}");
                if (hold.ExpiresAt.HasValue)
                {
                    sb.AppendLine($"      Expires: {hold.ExpiresAt.Value:yyyy-MM-dd}");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  No active legal holds.");
            sb.AppendLine();
        }
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"RECENT AUDIT HISTORY ({attestation.RecentAudits.Count} records)");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (attestation.RecentAudits.Any())
        {
            foreach (var audit in attestation.RecentAudits)
            {
                sb.AppendLine($"  {audit.ExecutedAt:yyyy-MM-dd HH:mm} | {audit.Status} | " +
                    $"{audit.FilesDeleted} files | {FormatBytes(audit.BytesFreed)} freed | {audit.ErrorCount} errors");
            }
        }
        else
        {
            sb.AppendLine("  No audit records available.");
        }
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("SYSTEM STATUS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"  Data Root:            {attestation.SystemStatus.DataRootPath}");
        sb.AppendLine($"  Symbols Configured:   {attestation.SystemStatus.TotalSymbolsConfigured}");
        sb.AppendLine($"  Application Version:  {attestation.SystemStatus.ApplicationVersion}");
        sb.AppendLine($"  Last Config Change:   {attestation.SystemStatus.LastConfigurationChange:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("ATTESTATION STATEMENT");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine(attestation.AttestationStatement);
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"                      End of Report - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }

    private void EnableScheduledAudits_Toggled(object sender, RoutedEventArgs e)
    {
        ScheduledAuditConfig.Opacity = EnableScheduledAuditsToggle.IsOn ? 1.0 : 0.5;
    }

    #endregion

    #region Helper Methods

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F1} {sizes[order]}";
    }

    #endregion
}

#region Attestation Models

/// <summary>
/// Compliance attestation report model.
/// </summary>
public class ComplianceAttestation
{
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public RetentionConfigurationSummary RetentionConfiguration { get; set; } = new();
    public GuardrailsSummary Guardrails { get; set; } = new();
    public List<LegalHoldSummary> ActiveLegalHolds { get; set; } = new();
    public List<AuditSummary> RecentAudits { get; set; } = new();
    public SystemStatusSummary SystemStatus { get; set; } = new();
    public string AttestationStatement { get; set; } = string.Empty;
}

/// <summary>
/// Retention configuration summary for attestation.
/// </summary>
public class RetentionConfigurationSummary
{
    public int TickDataRetentionDays { get; set; }
    public int BarDataRetentionDays { get; set; }
    public int QuoteDataRetentionDays { get; set; }
    public bool CompressBeforeDelete { get; set; }
    public bool ArchiveToCloud { get; set; }
}

/// <summary>
/// Guardrails summary for attestation.
/// </summary>
public class GuardrailsSummary
{
    public int MinTickDataDays { get; set; }
    public int MinBarDataDays { get; set; }
    public int MinQuoteDataDays { get; set; }
    public int MaxDailyDeletedFiles { get; set; }
    public bool RequireChecksumVerification { get; set; }
    public bool RequireDryRunPreview { get; set; }
}

/// <summary>
/// Legal hold summary for attestation.
/// </summary>
public class LegalHoldSummary
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int SymbolCount { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Audit summary for attestation.
/// </summary>
public class AuditSummary
{
    public DateTime ExecutedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public int ErrorCount { get; set; }
}

/// <summary>
/// System status summary for attestation.
/// </summary>
public class SystemStatusSummary
{
    public string DataRootPath { get; set; } = string.Empty;
    public int TotalSymbolsConfigured { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public DateTime LastConfigurationChange { get; set; }
}

#endregion
