using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Overrides;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecOverridePipelineTests
{
    [Fact]
    public void TryParseLine_AppliesTransformersInOrder_AndMapperSeesTransformedPayload()
    {
        var t1 = new RecordingTransformer((type, p) =>
        {
            var n = p.GetProperty("n").GetInt32();
            return (type, Parse($$"""{"timestamp":"2026-02-24T00:00:00Z","type":"demo","n":{{n + 1}}}"""));
        });

        var t2 = new RecordingTransformer((type, p) =>
        {
            var n = p.GetProperty("n").GetInt32();
            return (type, Parse($$"""{"timestamp":"2026-02-24T00:00:00Z","type":"demo","n":{{n + 1}}}"""));
        });

        var mapper = new RecordingMapper((timestamp, type, payload) =>
        {
            if (type != "demo")
            {
                return null;
            }

            var n = payload.GetProperty("n").GetInt32();
            return new CustomEvent
            {
                Timestamp = timestamp,
                Type = type,
                RawPayload = payload,
                N = n
            };
        });

        var options = Options.Create(new CodexClientOptions
        {
            EventTransformers = new IExecEventTransformer[] { t1, t2 },
            EventMappers = new IExecEventMapper[] { mapper }
        });

        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance, options);

        var ok = parser.TryParseLine(
            """{"timestamp":"2026-02-24T00:00:00Z","type":"demo","n":0}""",
            out var evt,
            out var error);

        ok.Should().BeTrue(error);
        evt.Should().BeOfType<CustomEvent>();
        ((CustomEvent)evt!).N.Should().Be(2);

        t1.Seen.Should().HaveCount(1);
        t1.Seen[0].GetProperty("n").GetInt32().Should().Be(0);

        t2.Seen.Should().HaveCount(1);
        t2.Seen[0].GetProperty("n").GetInt32().Should().Be(1);

        mapper.Seen.Should().HaveCount(1);
        mapper.Seen[0].GetProperty("n").GetInt32().Should().Be(2);
    }

    [Fact]
    public void TryParseLine_TransformsPayloadUsedByDefaultMapping()
    {
        var transformer = new RecordingTransformer((type, p) =>
            (type, Parse("""{"timestamp":"2026-02-24T00:00:00Z","type":"user_message","payload":{"message":"after"}}""")));

        var options = Options.Create(new CodexClientOptions
        {
            EventTransformers = new IExecEventTransformer[] { transformer }
        });

        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance, options);

        var ok = parser.TryParseLine(
            """{"timestamp":"2026-02-24T00:00:00Z","type":"user_message","payload":{"message":"before"}}""",
            out var evt,
            out var error);

        ok.Should().BeTrue(error);
        evt.Should().BeOfType<UserMessageEvent>();
        ((UserMessageEvent)evt!).Text.Should().Be("after");
    }

    [Fact]
    public void TryParseLine_SwallowsTransformerAndMapperExceptions()
    {
        var options = Options.Create(new CodexClientOptions
        {
            EventTransformers = new IExecEventTransformer[]
            {
                new ThrowingTransformer(),
                new RecordingTransformer((type, p) => (type, Parse("""{"timestamp":"2026-02-24T00:00:00Z","type":"demo","n":1}""")))
            },
            EventMappers = new IExecEventMapper[]
            {
                new ThrowingMapper(),
                new RecordingMapper((timestamp, type, payload) =>
                    new CustomEvent
                    {
                        Timestamp = timestamp,
                        Type = type,
                        RawPayload = payload,
                        N = payload.GetProperty("n").GetInt32()
                    })
            }
        });

        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance, options);

        CodexEvent? evt = null;
        string? error = null;
        var act = () => parser.TryParseLine(
            """{"timestamp":"2026-02-24T00:00:00Z","type":"demo","n":0}""",
            out evt,
            out error);

        act.Should().NotThrow();
        evt.Should().BeOfType<CustomEvent>();
        error.Should().BeNull();
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed record class CustomEvent : CodexEvent
    {
        public required int N { get; init; }
    }

    private sealed class RecordingTransformer : IExecEventTransformer
    {
        private readonly Func<string, JsonElement, (string Type, JsonElement RawPayload)> _transform;

        public List<JsonElement> Seen { get; } = new();

        public RecordingTransformer(Func<string, JsonElement, (string Type, JsonElement RawPayload)> transform)
        {
            _transform = transform;
        }

        public (string Type, JsonElement RawPayload) Transform(string type, JsonElement rawPayload)
        {
            Seen.Add(rawPayload);
            return _transform(type, rawPayload);
        }
    }

    private sealed class RecordingMapper : IExecEventMapper
    {
        private readonly Func<DateTimeOffset, string, JsonElement, CodexEvent?> _map;

        public List<JsonElement> Seen { get; } = new();

        public RecordingMapper(Func<DateTimeOffset, string, JsonElement, CodexEvent?> map)
        {
            _map = map;
        }

        public CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload)
        {
            Seen.Add(rawPayload);
            return _map(timestamp, type, rawPayload);
        }
    }

    private sealed class ThrowingTransformer : IExecEventTransformer
    {
        public (string Type, JsonElement RawPayload) Transform(string type, JsonElement rawPayload) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingMapper : IExecEventMapper
    {
        public CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload) =>
            throw new InvalidOperationException("boom");
    }
}
