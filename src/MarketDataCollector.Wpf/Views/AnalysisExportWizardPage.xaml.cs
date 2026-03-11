using System.Windows;
using System.Windows.Controls;
using MarketDataCollector.Wpf.ViewModels;

namespace MarketDataCollector.Wpf.Views;

public partial class AnalysisExportWizardPage : Page
{
    private readonly AnalysisExportWizardViewModel _viewModel = new();

    public AnalysisExportWizardPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
    }

    private void AddSymbol_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddSymbol();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoBack();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoNext();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelWizard();
    }
}
