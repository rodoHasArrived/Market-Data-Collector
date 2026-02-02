using System.Windows.Controls;

namespace MarketDataCollector.Wpf.Services;

public interface INavigationService
{
    Frame? Frame { get; set; }
    bool CanGoBack { get; }
    
    void NavigateTo<T>() where T : Page;
    void NavigateTo(Type pageType);
    void NavigateTo(string pageKey);
    void GoBack();
    void ClearHistory();
}
