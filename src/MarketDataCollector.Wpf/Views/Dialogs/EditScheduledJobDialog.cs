using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarketDataCollector.Wpf.Models;

namespace MarketDataCollector.Wpf.Views.Dialogs;

/// <summary>
/// Dialog for editing or deleting a scheduled backfill job.
/// </summary>
public sealed class EditScheduledJobDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _frequencyCombo;
    private readonly ComboBox _timeCombo;
    private readonly ComboBox _dayCombo;

    public string JobName => _nameBox.Text;
    public string NextRunText { get; private set; } = string.Empty;
    public bool ShouldDelete { get; private set; }

    public EditScheduledJobDialog(ScheduledJobInfo job)
    {
        Title = "Edit Scheduled Job";
        Width = 450;
        Height = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Job name
        AddLabel(grid, "Job Name:", 0);
        _nameBox = new TextBox
        {
            Text = job.Name,
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 4, 0, 12)
        };
        Grid.SetRow(_nameBox, 1);
        grid.Children.Add(_nameBox);

        // Frequency
        AddLabel(grid, "Frequency:", 2);
        _frequencyCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12)
        };
        _frequencyCombo.Items.Add("Daily");
        _frequencyCombo.Items.Add("Weekly");
        _frequencyCombo.Items.Add("Monthly");
        _frequencyCombo.SelectedIndex = job.Frequency == "Weekly" ? 1 : job.Frequency == "Monthly" ? 2 : 0;
        _frequencyCombo.SelectionChanged += OnFrequencyChanged;
        Grid.SetRow(_frequencyCombo, 3);
        grid.Children.Add(_frequencyCombo);

        // Time
        AddLabel(grid, "Time:", 4);
        _timeCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12)
        };
        for (var hour = 0; hour < 24; hour++)
        {
            _timeCombo.Items.Add($"{hour:D2}:00");
            _timeCombo.Items.Add($"{hour:D2}:30");
        }
        _timeCombo.SelectedIndex = 12; // 6:00 AM
        Grid.SetRow(_timeCombo, 5);
        grid.Children.Add(_timeCombo);

        // Day of week (for weekly)
        _dayCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 12),
            Visibility = job.Frequency == "Weekly" ? Visibility.Visible : Visibility.Collapsed
        };
        foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
        {
            _dayCombo.Items.Add(day);
        }
        _dayCombo.SelectedIndex = 6; // Sunday
        Grid.SetRow(_dayCombo, 6);
        grid.Children.Add(_dayCombo);

        // Buttons
        var buttonPanel = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(buttonPanel, 7);

        var deleteButton = new Button
        {
            Content = "Delete",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        deleteButton.Click += (_, _) => { ShouldDelete = true; DialogResult = true; Close(); };
        Grid.SetColumn(deleteButton, 0);
        buttonPanel.Children.Add(deleteButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        Grid.SetColumn(cancelButton, 2);
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save",
            Width = 100,
            Margin = new Thickness(8, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        saveButton.Click += OnSaveClick;
        Grid.SetColumn(saveButton, 3);
        buttonPanel.Children.Add(saveButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private void AddLabel(Grid grid, string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(label, row);
        grid.Children.Add(label);
    }

    private void OnFrequencyChanged(object sender, SelectionChangedEventArgs e)
    {
        _dayCombo.Visibility = _frequencyCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("Please enter a job name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Calculate next run text
        var time = _timeCombo.SelectedItem?.ToString() ?? "06:00";
        var frequency = _frequencyCombo.SelectedItem?.ToString() ?? "Daily";

        NextRunText = frequency switch
        {
            "Daily" => $"Tomorrow {time}",
            "Weekly" => $"{_dayCombo.SelectedItem} {time}",
            "Monthly" => $"1st of month {time}",
            _ => $"Tomorrow {time}"
        };

        DialogResult = true;
        Close();
    }
}
