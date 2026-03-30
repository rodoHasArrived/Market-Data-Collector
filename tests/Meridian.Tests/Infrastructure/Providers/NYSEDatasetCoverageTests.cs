using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Meridian.Application.Canonicalization;
using Meridian.Contracts.Catalog;
using Meridian.Contracts.Domain.Enums;
using Meridian.Contracts.Domain.Models;
using Meridian.Core.Serialization;
using Meridian.Domain.Events;
using NSubstitute;
using Xunit;

namespace Meridian.Tests.Infrastructure.Adapters;

public sealed class NYSEDatasetCoverageTests
{
    private readonly EventCanonicalizer _canonicalizer;

    public NYSEDatasetCoverageTests()
    {
        var repoRoot = ResolveRepoRoot();
        var conditionMapper = ConditionCodeMapper.LoadFromFile(
            Path.Combine(repoRoot, "config", "condition-codes.json"));
        var venueMapper = VenueMicMapper.LoadFromFile(
            Path.Combine(repoRoot, "config", "venue-mapping.json"));

        var registry = Substitute.For<ICanonicalSymbolRegistry>();
        registry.ResolveToCanonical(Arg.Any<string>()).Returns(c => c.Arg<string>());

        _canonicalizer = new EventCanonicalizer(registry, conditionMapper, venueMapper);
    }

    [Fact]
    public void NyseGoldenDataset_ContainsExpectedMessageMix()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(GetDatasetPath()));
        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToArray();

        messages.Should().HaveCount(4);
        messages.Count(m => m.GetProperty("type").GetString() == "trade").Should().Be(2);
        messages.Count(m => m.GetProperty("type").GetString() == "quote").Should().Be(1);
        messages.Count(m => m.GetProperty("type").GetString() == "depth").Should().Be(1);
    }

    [Fact]
    public void NyseGoldenDataset_TradeMessagesCanonicalizeVenueToMic()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(GetDatasetPath()));
        var tradeMessages = doc.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Where(m => m.GetProperty("type").GetString() == "trade")
            .ToArray();

        tradeMessages.Should().NotBeEmpty();

        foreach (var tradeMessage in tradeMessages)
        {
            var evt = BuildTradeEvent(tradeMessage);
            var canonical = _canonicalizer.Canonicalize(evt);

            canonical.CanonicalVenue.Should().Be("XNYS");
            canonical.CanonicalSymbol.Should().Be(evt.Symbol);
            canonical.Tier.Should().Be(MarketEventTier.CanonicalL1);
        }
    }

    [Fact]
    public void NyseGoldenDataset_CanonicalizedTradesRoundTripWithHighPerformanceJson()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(GetDatasetPath()));
        var canonicalizedTrades = doc.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Where(m => m.GetProperty("type").GetString() == "trade")
            .Select(BuildTradeEvent)
            .Select(_canonicalizer.Canonicalize)
            .ToArray();

        canonicalizedTrades.Should().HaveCount(2);

        foreach (var evt in canonicalizedTrades)
        {
            var json = HighPerformanceJson.Serialize(evt);
            var restored = HighPerformanceJson.Deserialize(json);

            restored.Should().NotBeNull();
            restored!.Symbol.Should().Be(evt.Symbol);
            restored.CanonicalVenue.Should().Be("XNYS");
            restored.Type.Should().Be(MarketEventType.Trade);
        }
    }

    private static MarketEvent BuildTradeEvent(JsonElement message)
    {
        var ts = DateTimeOffset.Parse(message.GetProperty("timestamp").GetString()!);
        var symbol = message.GetProperty("symbol").GetString()!;
        var seq = message.GetProperty("sequence").GetInt64();
        var price = message.GetProperty("price").GetDecimal();
        var size = message.GetProperty("size").GetInt64();
        var venue = message.GetProperty("exchange").GetString();
        var conditions = message.TryGetProperty("conditions", out var cond)
            ? new[] { cond.GetString()! }
            : null;

        var trade = new Trade(
            Timestamp: ts,
            Symbol: symbol,
            Price: price,
            Size: size,
            Aggressor: AggressorSide.Unknown,
            SequenceNumber: seq,
            StreamId: "NYSE",
            Venue: venue,
            RawConditions: conditions);

        return new MarketEvent(
            Timestamp: ts,
            Symbol: symbol,
            Type: MarketEventType.Trade,
            Payload: trade,
            Sequence: seq,
            Source: "NYSE",
            Tier: MarketEventTier.Raw,
            CanonicalizationVersion: 0);
    }

    private static string GetDatasetPath()
    {
        return Path.Combine(ResolveRepoRoot(), "tests", "Meridian.Tests", "TestData", "Golden", "nyse-feed-sample.json");
    }

    private static string ResolveRepoRoot()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var candidate = new DirectoryInfo(assemblyDir);
        while (candidate is not null && !File.Exists(Path.Combine(candidate.FullName, "Meridian.sln")))
            candidate = candidate.Parent;
        return candidate?.FullName ?? assemblyDir;
    }
}
