namespace MarketDataCollector.Wpf.Services;

public sealed class MessagingService : IMessagingService
{
    private readonly ILoggingService _logger;
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new();

    public MessagingService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        lock (_lock)
        {
            var messageType = typeof(TMessage);
            if (!_subscribers.ContainsKey(messageType))
            {
                _subscribers[messageType] = new List<Delegate>();
            }
            _subscribers[messageType].Add(handler);
            _logger.Log($"Subscribed to {messageType.Name}, total subscribers: {_subscribers[messageType].Count}");
        }
    }

    public void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        lock (_lock)
        {
            var messageType = typeof(TMessage);
            if (_subscribers.TryGetValue(messageType, out var handlers))
            {
                handlers.Remove(handler);
                _logger.Log($"Unsubscribed from {messageType.Name}, remaining subscribers: {handlers.Count}");
            }
        }
    }

    public void Publish<TMessage>(TMessage message) where TMessage : class
    {
        List<Delegate> handlers;
        var messageType = typeof(TMessage);
        
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(messageType, out var handlerList))
            {
                return;
            }
            handlers = new List<Delegate>(handlerList);
        }

        _logger.Log($"Publishing {messageType.Name} to {handlers.Count} subscribers");

        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Action<TMessage> typedHandler)
                {
                    typedHandler(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in message handler for {messageType.Name}: {ex.Message}", ex);
            }
        }
    }
}
