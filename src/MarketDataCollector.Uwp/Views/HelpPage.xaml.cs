using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using Windows.System;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for help, documentation, and onboarding.
/// </summary>
public sealed partial class HelpPage : Page
{
    private readonly ObservableCollection<TutorialStep> _tutorialSteps;
    private readonly ObservableCollection<FeatureCard> _featureCards;
    private int _currentTutorialStep;

    public HelpPage()
    {
        this.InitializeComponent();

        _tutorialSteps = new ObservableCollection<TutorialStep>();
        _featureCards = new ObservableCollection<FeatureCard>();

        TutorialStepsList.ItemsSource = _tutorialSteps;
        FeatureCardsGrid.ItemsSource = _featureCards;

        Loaded += HelpPage_Loaded;
    }

    private void HelpPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadTutorialSteps();
        LoadFeatureCards();
    }

    private void LoadTutorialSteps()
    {
        _tutorialSteps.Clear();

        _tutorialSteps.Add(new TutorialStep
        {
            StepNumber = "1",
            Title = "Select a Data Provider",
            Description = "Choose Interactive Brokers or Alpaca as your market data source.",
            IsCompleted = false
        });

        _tutorialSteps.Add(new TutorialStep
        {
            StepNumber = "2",
            Title = "Configure Credentials",
            Description = "Enter your API keys or configure TWS connection settings.",
            IsCompleted = false
        });

        _tutorialSteps.Add(new TutorialStep
        {
            StepNumber = "3",
            Title = "Add Symbol Subscriptions",
            Description = "Select which symbols to track for trades and market depth.",
            IsCompleted = false
        });

        _tutorialSteps.Add(new TutorialStep
        {
            StepNumber = "4",
            Title = "Configure Storage",
            Description = "Set up where and how your market data will be stored.",
            IsCompleted = false
        });

        _tutorialSteps.Add(new TutorialStep
        {
            StepNumber = "5",
            Title = "Start Collecting Data",
            Description = "Launch the collector service and begin capturing market data.",
            IsCompleted = false
        });
    }

    private void LoadFeatureCards()
    {
        _featureCards.Clear();

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE80F",
            Title = "Real-Time Dashboard",
            Description = "Monitor collection status and metrics in real-time"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE9D9",
            Title = "Symbol Management",
            Description = "Add, edit, and manage symbol subscriptions"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE8B7",
            Title = "Flexible Storage",
            Description = "Multiple naming conventions and compression options"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE787",
            Title = "Historical Backfill",
            Description = "Download historical data from multiple free providers"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uEDE1",
            Title = "Data Export",
            Description = "Export to CSV, Parquet, and QuantConnect formats"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE823",
            Title = "Trading Hours",
            Description = "Configure market sessions and holiday calendars"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE912",
            Title = "Service Manager",
            Description = "Control the collector service and view logs"
        });

        _featureCards.Add(new FeatureCard
        {
            Icon = "\uE72E",
            Title = "Secure Credentials",
            Description = "API keys stored in Windows Credential Manager"
        });
    }

    private async void StartTutorial_Click(object sender, RoutedEventArgs e)
    {
        TutorialProgressPanel.Visibility = Visibility.Visible;
        StartTutorialButton.Content = "Continue Tutorial";
        _currentTutorialStep = 0;

        await RunTutorialStep();
    }

    private async Task RunTutorialStep()
    {
        if (_currentTutorialStep < _tutorialSteps.Count)
        {
            TutorialProgressBar.Value = _currentTutorialStep;

            // Mark current step
            for (int i = 0; i < _tutorialSteps.Count; i++)
            {
                _tutorialSteps[i].IsCompleted = i < _currentTutorialStep;
                _tutorialSteps[i].IsCurrent = i == _currentTutorialStep;
            }

            // Simulate tutorial progress
            await Task.Delay(500);

            // Show guidance for current step
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = $"Step {_currentTutorialStep + 1}: {_tutorialSteps[_currentTutorialStep].Title}";
            ActionInfoBar.Message = _tutorialSteps[_currentTutorialStep].Description;
            ActionInfoBar.IsOpen = true;

            _currentTutorialStep++;
        }
        else
        {
            // Tutorial complete
            TutorialProgressBar.Value = _tutorialSteps.Count;
            foreach (var step in _tutorialSteps)
            {
                step.IsCompleted = true;
                step.IsCurrent = false;
            }

            ActionInfoBar.Severity = InfoBarSeverity.Success;
            ActionInfoBar.Title = "Tutorial Complete!";
            ActionInfoBar.Message = "You're all set to start collecting market data. Explore the navigation menu to access all features.";
            ActionInfoBar.IsOpen = true;

            StartTutorialButton.Content = "Restart Tutorial";
        }
    }

    private void FeatureSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.QueryText?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            LoadFeatureCards();
            return;
        }

        var filteredCards = new ObservableCollection<FeatureCard>();
        foreach (var card in _featureCards)
        {
            if (card.Title.ToLower().Contains(query) || card.Description.ToLower().Contains(query))
            {
                filteredCards.Add(card);
            }
        }

        FeatureCardsGrid.ItemsSource = filteredCards;

        if (filteredCards.Count == 0)
        {
            ActionInfoBar.Severity = InfoBarSeverity.Informational;
            ActionInfoBar.Title = "No Results";
            ActionInfoBar.Message = $"No features found matching '{query}'. Try different keywords.";
            ActionInfoBar.IsOpen = true;
        }
    }

    private async void OpenArchitectureDoc_Click(object sender, RoutedEventArgs e)
    {
        await OpenDocumentation("architecture.md");
    }

    private async void OpenConfigurationDoc_Click(object sender, RoutedEventArgs e)
    {
        await OpenDocumentation("configuration.md");
    }

    private async void OpenApiDoc_Click(object sender, RoutedEventArgs e)
    {
        await OpenDocumentation("domains.md");
    }

    private async void OpenLeanIntegrationDoc_Click(object sender, RoutedEventArgs e)
    {
        await OpenDocumentation("lean-integration.md");
    }

    private async void OpenIBSetupDoc_Click(object sender, RoutedEventArgs e)
    {
        await OpenDocumentation("interactive-brokers-setup.md");
    }

    private async void OpenOperatorRunbookDoc_Click(object sender, RoutedEventArgs e)
    {
        await OpenDocumentation("operator-runbook.md");
    }

    private async Task OpenDocumentation(string docFile)
    {
        ActionInfoBar.Severity = InfoBarSeverity.Informational;
        ActionInfoBar.Title = "Opening Documentation";
        ActionInfoBar.Message = $"Opening {docFile} in your default text editor...";
        ActionInfoBar.IsOpen = true;

        // In a real implementation, this would open the docs folder
        await Task.Delay(500);
    }
}

/// <summary>
/// Represents a tutorial step.
/// </summary>
public class TutorialStep
{
    public string StepNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }

    public SolidColorBrush StepBackground => IsCompleted
        ? BrushRegistry.Success
        : IsCurrent
            ? BrushRegistry.ChartPrimary
            : BrushRegistry.Inactive;

    public string StatusGlyph => IsCompleted ? "\uE73E" : IsCurrent ? "\uE768" : "\uE739";

    public SolidColorBrush StatusColor => IsCompleted
        ? BrushRegistry.Success
        : IsCurrent
            ? BrushRegistry.ChartPrimary
            : BrushRegistry.Inactive;
}

/// <summary>
/// Represents a feature discovery card.
/// </summary>
public class FeatureCard
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
