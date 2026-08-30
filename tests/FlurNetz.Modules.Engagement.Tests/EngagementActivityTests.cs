using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Serialization;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Contracts;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Engagement.Tests;

public sealed class EngagementActivityIdTests
{
    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var id = EngagementActivityId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => EngagementActivityId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var first = EngagementActivityId.Create(value);
        var second = EngagementActivityId.Create(value);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = EngagementActivityId.Create(Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06"));
        var second = EngagementActivityId.Create(Guid.Parse("8aa2a9f7-5e44-4ec1-bd46-0d9cb71bdc79"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = EngagementActivityId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }
}

public sealed class EngagementActivityTests
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateMessage_CarriesAllProvidedValues()
    {
        var activityId = EngagementActivityId.New();
        var communityIdentityId = CommunityIdentityId.New();

        var activity = EngagementActivity.CreateMessage(activityId, communityIdentityId, OccurredAtUtc);

        Assert.Equal(activityId, activity.Id);
        Assert.Equal(communityIdentityId, activity.CommunityIdentityId);
        Assert.Equal(EngagementActivityType.Message, activity.Type);
        Assert.Equal(OccurredAtUtc, activity.OccurredAtUtc);
    }

    [Fact]
    public void CreateMessage_RejectsInvalidIds()
    {
        var activityId = EngagementActivityId.New();
        var communityIdentityId = CommunityIdentityId.New();

        Assert.Throws<ArgumentException>(() =>
            EngagementActivity.CreateMessage(default, communityIdentityId, OccurredAtUtc));
        Assert.Throws<ArgumentException>(() =>
            EngagementActivity.CreateMessage(activityId, default, OccurredAtUtc));
    }

    [Fact]
    public void CreateMessage_RejectsNonUtcTimestamp()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 29, 14, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() =>
            EngagementActivity.CreateMessage(
                EngagementActivityId.New(),
                CommunityIdentityId.New(),
                nonUtc));
    }

    [Fact]
    public void Values_AreExposedWithoutSetters()
    {
        var properties = typeof(EngagementActivity)
            .GetProperties()
            .Where(property => property.DeclaringType == typeof(EngagementActivity))
            .ToArray();

        Assert.Equal(
            [
                nameof(EngagementActivity.Id),
                nameof(EngagementActivity.CommunityIdentityId),
                nameof(EngagementActivity.Type),
                nameof(EngagementActivity.OccurredAtUtc)
            ],
            properties.Select(property => property.Name).ToArray());
        Assert.All(properties, property => Assert.Null(property.SetMethod));
    }
}

public sealed class RecordMessageEngagementTests
{
    private static readonly DateTimeOffset OccurredAtUtc =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_PersistsMessageUsingTheClockTimestamp()
    {
        var recorder = new RecordingMessageEngagementRecorder();
        var useCase = new RecordMessageEngagement(recorder, new FixedClock(OccurredAtUtc));
        var communityIdentityId = CommunityIdentityId.New();

        var id = await useCase.ExecuteAsync(communityIdentityId, TestContext.Current.CancellationToken);

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(id, activity.Id);
        Assert.Equal(communityIdentityId, activity.CommunityIdentityId);
        Assert.Equal(EngagementActivityType.Message, activity.Type);
        Assert.Equal(OccurredAtUtc, activity.OccurredAtUtc);

        var envelope = Assert.Single(recorder.Envelopes);
        var integrationEvent = Assert.IsType<MessageEngagementRecordedIntegrationEvent>(envelope.Payload);
        Assert.Equal(MessageEngagementRecordedIntegrationEvent.MessageType, envelope.MessageType);
        Assert.Equal(MessageEngagementRecordedIntegrationEvent.SchemaVersion, envelope.SchemaVersion);
        Assert.Equal(OccurredAtUtc, envelope.OccurredAtUtc);
        Assert.Equal(communityIdentityId.Value, integrationEvent.CommunityIdentityId);
        Assert.NotEqual(Guid.Empty, envelope.MessageId);
        Assert.Null(envelope.CorrelationId);
        Assert.Null(envelope.CausationId);
        Assert.Equal(TestContext.Current.CancellationToken, recorder.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesDistinctActivitiesForSeparateExecutions()
    {
        var recorder = new RecordingMessageEngagementRecorder();
        var useCase = new RecordMessageEngagement(recorder, new FixedClock(OccurredAtUtc));
        var communityIdentityId = CommunityIdentityId.New();

        var first = await useCase.ExecuteAsync(communityIdentityId, TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(communityIdentityId, TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
        Assert.Equal(2, recorder.Activities.Count);
        Assert.Equal(2, recorder.Envelopes.Select(envelope => envelope.MessageId).Distinct().Count());
    }

    private sealed class RecordingMessageEngagementRecorder : IMessageEngagementRecorder
    {
        public List<EngagementActivity> Activities { get; } = [];

        public List<IntegrationEventEnvelope> Envelopes { get; } = [];

        public CancellationToken CancellationToken { get; private set; }

        public Task RecordAsync(
            EngagementActivity activity,
            IntegrationEventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Activities.Add(activity);
            Envelopes.Add(envelope);
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}

public sealed class MessageEngagementRecordedIntegrationEventTests
{
    [Fact]
    public void ConstructorRejectsEmptyCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() =>
            new MessageEngagementRecordedIntegrationEvent(Guid.Empty));
    }

    [Fact]
    public void ContractExposesStableMessageTypeAndSchemaVersion()
    {
        Assert.Equal("engagement.message-recorded", MessageEngagementRecordedIntegrationEvent.MessageType);
        Assert.Equal(1, MessageEngagementRecordedIntegrationEvent.SchemaVersion);
    }

    [Fact]
    public void SerializerRoundTripsOnlyTheInternalCommunityIdentityId()
    {
        var communityIdentityId = Guid.NewGuid();
        var registry = new IntegrationEventTypeRegistry();
        registry.Register<MessageEngagementRecordedIntegrationEvent>(
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion);
        var serializer = new IntegrationEventJsonSerializer(registry);
        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            MessageEngagementRecordedIntegrationEvent.MessageType,
            MessageEngagementRecordedIntegrationEvent.SchemaVersion,
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero),
            new MessageEngagementRecordedIntegrationEvent(communityIdentityId));

        var serialized = serializer.Serialize(envelope);
        var payload = System.Text.Json.JsonDocument.Parse(serialized.Payload).RootElement;
        var deserialized = Assert.IsType<MessageEngagementRecordedIntegrationEvent>(
            serializer.Deserialize(serialized));

        Assert.Equal(communityIdentityId, deserialized.CommunityIdentityId);
        var properties = payload.EnumerateObject().ToArray();
        Assert.Single(properties);
        Assert.Equal("communityIdentityId", properties[0].Name);
        Assert.Equal(communityIdentityId, properties[0].Value.GetGuid());
    }
}
