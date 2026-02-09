namespace MarketDataCollector.Ui.Services.Contracts;

/// <summary>
/// Interface for in-process pub/sub messaging between UI components.
/// Shared between WPF and UWP desktop applications.
/// Part of C1 improvement (WPF/UWP service deduplication).
/// </summary>
public interface IMessagingService
{
    void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
    void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
    void Publish<TMessage>(TMessage message) where TMessage : class;
}
