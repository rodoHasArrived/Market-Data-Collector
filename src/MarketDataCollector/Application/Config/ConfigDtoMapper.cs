using MarketDataCollector.Contracts.Configuration;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// Maps shared configuration DTOs to core configuration records.
/// </summary>
public static class ConfigDtoMapper
{
    public static AlpacaOptions? ToDomain(this AlpacaOptionsDto? dto)
        => dto is null
            ? null
            : new AlpacaOptions(
                KeyId: dto.KeyId ?? string.Empty,
                SecretKey: dto.SecretKey ?? string.Empty,
                Feed: dto.Feed,
                UseSandbox: dto.UseSandbox,
                SubscribeQuotes: dto.SubscribeQuotes);

    public static PolygonOptions? ToDomain(this PolygonOptionsDto? dto)
        => dto is null
            ? null
            : new PolygonOptions(
                ApiKey: dto.ApiKey,
                UseDelayed: dto.UseDelayed,
                Feed: dto.Feed,
                SubscribeTrades: dto.SubscribeTrades,
                SubscribeQuotes: dto.SubscribeQuotes,
                SubscribeAggregates: dto.SubscribeAggregates);

    public static IBOptions? ToDomain(this IBOptionsDto? dto)
        => dto is null
            ? null
            : new IBOptions(
                Host: dto.Host,
                Port: dto.Port,
                ClientId: dto.ClientId,
                UsePaperTrading: dto.UsePaperTrading,
                SubscribeDepth: dto.SubscribeDepth,
                DepthLevels: dto.DepthLevels,
                TickByTick: dto.TickByTick);
}
