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
    [Fact]
    public void Create_CarriesBothProvidedIds()
    {
        var activityId = EngagementActivityId.New();
        var communityIdentityId = CommunityIdentityId.New();

        var activity = EngagementActivity.Create(activityId, communityIdentityId);

        Assert.Equal(activityId, activity.Id);
        Assert.Equal(communityIdentityId, activity.CommunityIdentityId);
    }

    [Fact]
    public void Create_RejectsAnInvalidEngagementActivityId()
    {
        var communityIdentityId = CommunityIdentityId.New();

        Assert.Throws<ArgumentException>(() =>
            EngagementActivity.Create(default, communityIdentityId));
    }

    [Fact]
    public void Create_RejectsAnInvalidCommunityIdentityId()
    {
        var activityId = EngagementActivityId.New();

        Assert.Throws<ArgumentException>(() =>
            EngagementActivity.Create(activityId, default));
    }

    [Fact]
    public void Ids_AreExposedWithoutSetters()
    {
        var idProperty = typeof(EngagementActivity).GetProperty(nameof(EngagementActivity.Id));
        var communityIdentityIdProperty = typeof(EngagementActivity)
            .GetProperty(nameof(EngagementActivity.CommunityIdentityId));

        Assert.NotNull(idProperty);
        Assert.Null(idProperty!.SetMethod);
        Assert.NotNull(communityIdentityIdProperty);
        Assert.Null(communityIdentityIdProperty!.SetMethod);
    }
}
