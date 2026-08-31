using FlurNetz.Messaging.Domain;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Serialization;

namespace FlurNetz.Messaging.Tests;

/// <summary>
/// Prüft die frameworkfreie In-Process-Domain-Event-Grundlage sowie Registry und Serializer.
/// </summary>
public sealed class MessagingTests
{
    [Fact]
    public async Task DomainDispatcherRunsMatchingHandlersInRegistrationOrder()
    {
        var calls = new List<string>();
        var dispatcher = new DomainEventDispatcher(
        [
            new DomainEventHandlerRegistration<TestDomainEvent>(new RecordingDomainHandler("first", calls)),
            new DomainEventHandlerRegistration<TestDomainEvent>(new RecordingDomainHandler("second", calls)),
            new DomainEventHandlerRegistration<OtherDomainEvent>(new RecordingOtherDomainHandler(calls))
        ]);

        await dispatcher.DispatchAsync(new TestDomainEvent("payload"), TestContext.Current.CancellationToken);

        Assert.Equal(["first:payload", "second:payload"], calls);
    }

    [Fact]
    public async Task DomainDispatcherTreatsMissingHandlerAsNoOp()
    {
        var dispatcher = new DomainEventDispatcher([]);

        await dispatcher.DispatchAsync(new TestDomainEvent("payload"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DomainDispatcherPropagatesHandlerFailure()
    {
        var dispatcher = new DomainEventDispatcher(
        [
            new DomainEventHandlerRegistration<TestDomainEvent>(new FailingDomainHandler())
        ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(new TestDomainEvent("payload"), TestContext.Current.CancellationToken));

        Assert.Equal("synthetic domain failure", exception.Message);
    }

    [Fact]
    public async Task DomainDispatcherHonorsCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new RecordingDomainHandler("never", []);
        var dispatcher = new DomainEventDispatcher(
        [new DomainEventHandlerRegistration<TestDomainEvent>(handler)]);

        #pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new TestDomainEvent("payload"), cancellation.Token));
        #pragma warning restore xUnit1051
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void RegistryResolvesRegisteredTypeAndVersion()
    {
        var registry = new IntegrationEventTypeRegistry();

        registry.Register<TestIntegrationEvent>("test.integration", 1);

        var descriptor = registry.Resolve("test.integration", 1);

        Assert.Equal("test.integration", descriptor.MessageType);
        Assert.Equal(1, descriptor.SchemaVersion);
        Assert.Equal(typeof(TestIntegrationEvent), descriptor.ClrType);
        Assert.Same(descriptor, registry.Resolve(typeof(TestIntegrationEvent)));
    }

    [Fact]
    public void RegistryRejectsDuplicateMessageTypeAndVersion()
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<TestIntegrationEvent>("test.integration", 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Register<OtherIntegrationEvent>("test.integration", 1));

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryRejectsUnknownTypeAndVersionClearly()
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<TestIntegrationEvent>("test.integration", 1);

        Assert.Throws<UnknownIntegrationEventTypeException>(
            () => registry.Resolve("missing.integration", 1));
        Assert.Throws<UnknownIntegrationEventVersionException>(
            () => registry.Resolve("test.integration", 2));
    }

    [Fact]
    public void IntegrationEventEnvelopeRetainsNormalizedCorrelationAndCausationIds()
    {
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            "test.integration",
            1,
            new DateTimeOffset(2026, 8, 31, 16, 15, 0, TimeSpan.Zero),
            new TestIntegrationEvent("hello", 42),
            " correlation-123 ",
            " causation-456 ");

        Assert.Equal(" correlation-123 ", envelope.CorrelationId);
        Assert.Equal(" causation-456 ", envelope.CausationId);

        var blankEnvelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            "test.integration",
            1,
            new DateTimeOffset(2026, 8, 31, 16, 15, 0, TimeSpan.Zero),
            new TestIntegrationEvent("hello", 42),
            "   ",
            "\t");

        Assert.Null(blankEnvelope.CorrelationId);
        Assert.Null(blankEnvelope.CausationId);
    }

    [Fact]
    public void SerializerRoundTripsRegisteredPayloadWithoutClrWireType()
    {
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<TestIntegrationEvent>("test.integration", 1);
        var serializer = new IntegrationEventJsonSerializer(registry);
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            "test.integration",
            1,
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero),
            new TestIntegrationEvent("hello", 42));

        var serialized = serializer.Serialize(envelope);
        var deserialized = Assert.IsType<TestIntegrationEvent>(serializer.Deserialize(serialized));

        Assert.Equal("test.integration", serialized.MessageType);
        Assert.Equal(1, serialized.SchemaVersion);
        Assert.Equal("hello", deserialized.Text);
        Assert.Equal(42, deserialized.Number);
        Assert.DoesNotContain("AssemblyQualifiedName", System.Text.Encoding.UTF8.GetString(serialized.Payload), StringComparison.Ordinal);
    }

    private sealed record TestDomainEvent(string Text) : IDomainEvent;

    private sealed record OtherDomainEvent : IDomainEvent;

    private sealed record TestIntegrationEvent(string Text, int Number) : IIntegrationEvent;

    private sealed record OtherIntegrationEvent(string Text) : IIntegrationEvent;

    private sealed class RecordingDomainHandler(string name, ICollection<string> calls)
        : IDomainEventHandler<TestDomainEvent>
    {
        public IReadOnlyCollection<string> Calls => calls.ToArray();

        public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken = default)
        {
            calls.Add($"{name}:{@event.Text}");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOtherDomainHandler(ICollection<string> calls)
        : IDomainEventHandler<OtherDomainEvent>
    {
        public Task HandleAsync(OtherDomainEvent @event, CancellationToken cancellationToken = default)
        {
            calls.Add("other");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingDomainHandler : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("synthetic domain failure");
        }
    }
}
