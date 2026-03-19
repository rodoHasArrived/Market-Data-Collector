using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MarketDataCollector.Wpf.Views.Dialogs;

/// <summary>
/// Dialog for configuring provider API keys.
/// </summary>
public sealed class ApiKeyDialog : Window
{
    private readonly PasswordBox _apiKeyBox;

    public string ApiKey => _apiKeyBox.Password;

    public ApiKeyDialog(string providerName, string envVarName, bool isOptional = false)
    {
        Title = $"Configure {providerName} API Key";
        Width = 450;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Description
        var descText = new TextBlock
        {
            Text = $"Enter your {providerName} API key{(isOptional ? " (optional)" : "")}:",
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(descText, 0);
        grid.Children.Add(descText);

        // Environment variable hint
        var hintText = new TextBlock
        {
            Text = $"Environment variable: {envVarName}",
            Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(hintText, 1);
        grid.Children.Add(hintText);

        // API Key input — uses PasswordBox so the value is masked
        _apiKeyBox = new PasswordBox
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 62)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Padding = new Thickness(10, 8, 10, 8),
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 0, 0, 16)
        };

        Grid.SetRow(_apiKeyBox, 2);
        grid.Children.Add(_apiKeyBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 4);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(58, 58, 78)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelButton.Click += (_, _) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        var saveButton = new Button
        {
            Content = "Save",
            Width = 100,
            Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        saveButton.Click += OnSaveClick;
        buttonPanel.Children.Add(saveButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
