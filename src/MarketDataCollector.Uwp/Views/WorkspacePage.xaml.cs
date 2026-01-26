using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Uwp.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Workspace management page for creating, editing, and switching workspaces.
/// Implements Feature Refinement #51 - Workspace Templates & Session Restore.
/// </summary>
public sealed partial class WorkspacePage : Page
{
    private readonly WorkspaceService _workspaceService;
    private readonly NotificationService _notificationService;

    public WorkspacePage()
    {
        this.InitializeComponent();
        _workspaceService = WorkspaceService.Instance;
        _notificationService = NotificationService.Instance;

        // Subscribe to workspace events
        _workspaceService.WorkspaceCreated += WorkspaceService_WorkspaceChanged;
        _workspaceService.WorkspaceUpdated += WorkspaceService_WorkspaceChanged;
        _workspaceService.WorkspaceDeleted += WorkspaceService_WorkspaceChanged;
        _workspaceService.WorkspaceActivated += WorkspaceService_WorkspaceActivated;

        Loaded += WorkspacePage_Loaded;
        Unloaded += WorkspacePage_Unloaded;
    }

    private void WorkspacePage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = LoadDataAsync();
        LoadAutoRestoreSetting();
    }

    private void WorkspacePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _workspaceService.WorkspaceCreated -= WorkspaceService_WorkspaceChanged;
        _workspaceService.WorkspaceUpdated -= WorkspaceService_WorkspaceChanged;
        _workspaceService.WorkspaceDeleted -= WorkspaceService_WorkspaceChanged;
        _workspaceService.WorkspaceActivated -= WorkspaceService_WorkspaceActivated;
    }

    private void WorkspaceService_WorkspaceChanged(object? sender, WorkspaceEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = LoadDataAsync());
    }

    private void WorkspaceService_WorkspaceActivated(object? sender, WorkspaceEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateActiveWorkspaceDisplay();
            _ = _notificationService.NotifyAsync(
                "Workspace Activated",
                $"Switched to {e.Workspace?.Name} workspace",
                NotificationType.Info);
        });
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await _workspaceService.LoadWorkspacesAsync();

            // Update active workspace display
            UpdateActiveWorkspaceDisplay();

            // Update session restore display
            UpdateSessionRestoreDisplay();

            // Populate built-in workspaces
            var builtInWorkspaces = _workspaceService.Workspaces.Where(w => w.IsBuiltIn).ToList();
            BuiltInWorkspacesGrid.ItemsSource = builtInWorkspaces;

            // Populate custom workspaces
            var customWorkspaces = _workspaceService.Workspaces.Where(w => !w.IsBuiltIn).ToList();
            CustomWorkspacesList.ItemsSource = customWorkspaces;
            CustomWorkspaceCount.Text = $"({customWorkspaces.Count})";
            NoCustomWorkspacesText.Visibility = customWorkspaces.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CustomWorkspacesList.Visibility = customWorkspaces.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkspacePage] Error loading data: {ex.Message}");
        }
    }

    private void UpdateActiveWorkspaceDisplay()
    {
        var activeWorkspace = _workspaceService.ActiveWorkspace;
        if (activeWorkspace != null)
        {
            ActiveWorkspaceContent.Visibility = Visibility.Visible;
            NoActiveWorkspaceText.Visibility = Visibility.Collapsed;

            ActiveWorkspaceName.Text = activeWorkspace.Name;
            ActiveWorkspaceDescription.Text = activeWorkspace.Description;
            ActiveWorkspacePages.Text = $"{activeWorkspace.Pages.Count} pages";
            ActiveWorkspaceUpdated.Text = $"Updated: {activeWorkspace.UpdatedAt:g}";
        }
        else
        {
            ActiveWorkspaceContent.Visibility = Visibility.Collapsed;
            NoActiveWorkspaceText.Visibility = Visibility.Visible;
        }
    }

    private void UpdateSessionRestoreDisplay()
    {
        var lastSession = _workspaceService.LastSession;
        if (lastSession != null)
        {
            LastSessionContent.Visibility = Visibility.Visible;
            NoSessionText.Visibility = Visibility.Collapsed;

            LastSessionTime.Text = lastSession.SavedAt.ToString("g");
            LastSessionPage.Text = lastSession.ActivePageTag;
            LastSessionPages.Text = $"{lastSession.OpenPages.Count} open pages";
        }
        else
        {
            LastSessionContent.Visibility = Visibility.Collapsed;
            NoSessionText.Visibility = Visibility.Visible;
        }
    }

    private void LoadAutoRestoreSetting()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue("AutoRestoreSession", out var value))
            {
                AutoRestoreToggle.IsOn = (bool)value;
            }
        }
        catch
        {
            AutoRestoreToggle.IsOn = true;
        }
    }

    private void AutoRestoreToggle_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["AutoRestoreSession"] = AutoRestoreToggle.IsOn;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkspacePage] Error saving auto-restore setting: {ex.Message}");
        }
    }

    #region Event Handlers

    private async void CreateWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Create Workspace",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var panel = new StackPanel { Spacing = 16 };
        var nameBox = new TextBox { Header = "Name", PlaceholderText = "My Workspace" };
        var descriptionBox = new TextBox
        {
            Header = "Description",
            PlaceholderText = "Description of this workspace",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };
        var categoryCombo = new ComboBox { Header = "Category" };
        categoryCombo.Items.Add(new ComboBoxItem { Content = "Monitoring", Tag = WorkspaceCategory.Monitoring });
        categoryCombo.Items.Add(new ComboBoxItem { Content = "Backfill", Tag = WorkspaceCategory.Backfill });
        categoryCombo.Items.Add(new ComboBoxItem { Content = "Storage", Tag = WorkspaceCategory.Storage });
        categoryCombo.Items.Add(new ComboBoxItem { Content = "Analysis", Tag = WorkspaceCategory.Analysis });
        categoryCombo.Items.Add(new ComboBoxItem { Content = "Custom", Tag = WorkspaceCategory.Custom, IsSelected = true });

        panel.Children.Add(nameBox);
        panel.Children.Add(descriptionBox);
        panel.Children.Add(categoryCombo);
        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            var category = (categoryCombo.SelectedItem as ComboBoxItem)?.Tag is WorkspaceCategory c
                ? c
                : WorkspaceCategory.Custom;

            await _workspaceService.CreateWorkspaceAsync(nameBox.Text, descriptionBox.Text, category);
            await _notificationService.NotifyAsync("Workspace Created", $"Created '{nameBox.Text}' workspace", NotificationType.Success);
        }
    }

    private async void ImportWorkspace_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var json = await FileIO.ReadTextAsync(file);
                var workspace = await _workspaceService.ImportWorkspaceAsync(json);
                if (workspace != null)
                {
                    await _notificationService.NotifyAsync("Import Successful", $"Imported '{workspace.Name}' workspace", NotificationType.Success);
                }
                else
                {
                    await _notificationService.NotifyErrorAsync("Import Failed", "Could not parse workspace file");
                }
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Import Failed", ex.Message);
        }
    }

    private void SwitchWorkspace_Click(object sender, RoutedEventArgs e)
    {
        // Show workspace switcher flyout or dialog
        ShowWorkspaceSwitcher();
    }

    private async void ShowWorkspaceSwitcher()
    {
        var dialog = new ContentDialog
        {
            Title = "Switch Workspace",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = _workspaceService.Workspaces
        };

        listView.ItemTemplate = (DataTemplate)Resources["WorkspaceSwitcherItemTemplate"] ??
            CreateWorkspaceSwitcherTemplate();

        listView.SelectionChanged += async (s, args) =>
        {
            if (args.AddedItems.FirstOrDefault() is WorkspaceTemplate workspace)
            {
                await _workspaceService.ActivateWorkspaceAsync(workspace.Id);
                dialog.Hide();
            }
        };

        dialog.Content = listView;
        await dialog.ShowAsync();
    }

    private DataTemplate CreateWorkspaceSwitcherTemplate()
    {
        // Fallback template creation
        return null!;
    }

    private async void UpdateActiveWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var activeWorkspace = _workspaceService.ActiveWorkspace;
        if (activeWorkspace != null)
        {
            // Capture current state and update workspace
            var currentState = GetCurrentSessionState();
            activeWorkspace.Pages = currentState.OpenPages;
            activeWorkspace.WidgetLayout = currentState.WidgetLayout;
            activeWorkspace.Filters = currentState.ActiveFilters;

            await _workspaceService.UpdateWorkspaceAsync(activeWorkspace);
            await _notificationService.NotifyAsync("Workspace Updated", $"Updated '{activeWorkspace.Name}' with current state", NotificationType.Success);
        }
    }

    private async void ActivateWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workspaceId)
        {
            await _workspaceService.ActivateWorkspaceAsync(workspaceId);
        }
    }

    private async void EditWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workspaceId)
        {
            var workspace = _workspaceService.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
            if (workspace != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Edit Workspace",
                    PrimaryButtonText = "Save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var panel = new StackPanel { Spacing = 16 };
                var nameBox = new TextBox { Header = "Name", Text = workspace.Name };
                var descriptionBox = new TextBox
                {
                    Header = "Description",
                    Text = workspace.Description,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Height = 80
                };

                panel.Children.Add(nameBox);
                panel.Children.Add(descriptionBox);
                dialog.Content = panel;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    workspace.Name = nameBox.Text;
                    workspace.Description = descriptionBox.Text;
                    await _workspaceService.UpdateWorkspaceAsync(workspace);
                }
            }
        }
    }

    private async void ExportWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workspaceId)
        {
            try
            {
                var json = await _workspaceService.ExportWorkspaceAsync(workspaceId);
                if (!string.IsNullOrEmpty(json))
                {
                    var workspace = _workspaceService.Workspaces.FirstOrDefault(w => w.Id == workspaceId);

                    var picker = new FileSavePicker();
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
                    picker.SuggestedFileName = $"workspace_{workspace?.Name ?? "export"}_{DateTime.Now:yyyyMMdd}";

                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        await FileIO.WriteTextAsync(file, json);
                        await _notificationService.NotifyAsync("Export Complete", $"Exported to {file.Name}", NotificationType.Success);
                    }
                }
            }
            catch (Exception ex)
            {
                await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
            }
        }
    }

    private async void DeleteWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workspaceId)
        {
            var workspace = _workspaceService.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
            if (workspace != null && !workspace.IsBuiltIn)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Workspace",
                    Content = $"Are you sure you want to delete '{workspace.Name}'? This cannot be undone.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await _workspaceService.DeleteWorkspaceAsync(workspaceId);
                    await _notificationService.NotifyAsync("Workspace Deleted", $"Deleted '{workspace.Name}'", NotificationType.Info);
                }
            }
        }
    }

    private async void CaptureCurrentState_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Capture Current State",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var panel = new StackPanel { Spacing = 16 };
        var nameBox = new TextBox { Header = "Workspace Name", PlaceholderText = "My Custom Workspace" };
        var descriptionBox = new TextBox
        {
            Header = "Description",
            PlaceholderText = "What is this workspace for?",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };

        panel.Children.Add(nameBox);
        panel.Children.Add(descriptionBox);
        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            await _workspaceService.CaptureCurrentStateAsync(nameBox.Text, descriptionBox.Text);
            await _notificationService.NotifyAsync("Workspace Saved", $"Saved current state as '{nameBox.Text}'", NotificationType.Success);
        }
    }

    private void RestoreSession_Click(object sender, RoutedEventArgs e)
    {
        var lastSession = _workspaceService.LastSession;
        if (lastSession != null)
        {
            // Navigate to the last active page
            if (Frame.Parent is NavigationView navView)
            {
                // Find and select the navigation item
                foreach (var item in navView.MenuItems.OfType<NavigationViewItem>())
                {
                    if (item.Tag?.ToString() == lastSession.ActivePageTag)
                    {
                        navView.SelectedItem = item;
                        break;
                    }
                }
            }
        }
    }

    private async void SaveCurrentSession_Click(object sender, RoutedEventArgs e)
    {
        var state = GetCurrentSessionState();
        await _workspaceService.SaveSessionStateAsync(state);
        UpdateSessionRestoreDisplay();
        await _notificationService.NotifyAsync("Session Saved", "Current session state has been saved", NotificationType.Success);
    }

    private SessionState GetCurrentSessionState()
    {
        // Get current page info from navigation
        var currentPage = "Dashboard";
        if (Frame.CurrentSourcePageType != null)
        {
            currentPage = Frame.CurrentSourcePageType.Name.Replace("Page", "");
        }

        return new SessionState
        {
            ActivePageTag = currentPage,
            OpenPages = new List<WorkspacePage>
            {
                new WorkspacePage { PageTag = currentPage, Title = currentPage, IsDefault = true }
            },
            WidgetLayout = new Dictionary<string, WidgetPosition>(),
            ActiveFilters = new Dictionary<string, string>(),
            SavedAt = DateTime.UtcNow
        };
    }

    #endregion
}
