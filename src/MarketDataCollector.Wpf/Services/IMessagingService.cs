namespace MarketDataCollector.Wpf.Services;

public interface IMessagingService
{
    void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
    void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
    void Publish<TMessage>(TMessage message) where TMessage : class;
}
