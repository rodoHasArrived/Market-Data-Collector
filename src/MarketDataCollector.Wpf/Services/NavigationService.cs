using System.Windows.Controls;

namespace MarketDataCollector.Wpf.Services;

public sealed class NavigationService : INavigationService
{
    private readonly ILoggingService _logger;
    private readonly Dictionary<string, Type> _pageRegistry = new();
    private readonly IServiceProvider _serviceProvider;

    public Frame? Frame { get; set; }
    
    public bool CanGoBack => Frame?.CanGoBack ?? false;

    public NavigationService(IServiceProvider serviceProvider, ILoggingService logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void RegisterPage<T>(string key) where T : Page
    {
        _pageRegistry[key] = typeof(T);
        _logger.Log($"Registered page: {key} -> {typeof(T).Name}");
    }

    public void NavigateTo<T>() where T : Page
    {
        NavigateTo(typeof(T));
    }

    public void NavigateTo(Type pageType)
    {
        if (Frame == null)
        {
            _logger.Log("Cannot navigate: Frame is null");
            return;
        }

        try
        {
            _logger.Log($"Navigating to {pageType.Name}");
            var page = _serviceProvider.GetService(pageType) as Page 
                      ?? Activator.CreateInstance(pageType) as Page;
            
            if (page != null)
            {
                Frame.Navigate(page);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Navigation failed: {ex.Message}");
        }
    }

    public void NavigateTo(string pageKey)
    {
        if (_pageRegistry.TryGetValue(pageKey, out var pageType))
        {
            NavigateTo(pageType);
        }
        else
        {
            _logger.Log($"Page key not found: {pageKey}");
        }
    }

    public void GoBack()
    {
        if (CanGoBack)
        {
            _logger.Log("Navigating back");
            Frame?.GoBack();
        }
    }

    public void ClearHistory()
    {
        if (Frame != null)
        {
            _logger.Log("Clearing navigation history");
            while (Frame.CanGoBack)
            {
                Frame.RemoveBackEntry();
            }
        }
    }
}
