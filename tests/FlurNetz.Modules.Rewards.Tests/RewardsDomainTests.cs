using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Rewards.Domain;
using System.Collections.Generic;

namespace FlurNetz.Modules.Rewards.Tests;

public sealed class RewardDefinitionIdTests
{
    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = RewardDefinitionId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var id = RewardDefinitionId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => RewardDefinitionId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        Assert.Equal(RewardDefinitionId.Create(value), RewardDefinitionId.Create(value));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = RewardDefinitionId.Create(Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06"));
        var second = RewardDefinitionId.Create(Guid.Parse("8aa2a9f7-5e44-4ec1-bd46-0d9cb71bdc79"));

        Assert.NotEqual(first, second);
    }
}

public sealed class RewardPackageIdTests
{
    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = RewardPackageId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var id = RewardPackageId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => RewardPackageId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        Assert.Equal(RewardPackageId.Create(value), RewardPackageId.Create(value));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = RewardPackageId.Create(Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06"));
        var second = RewardPackageId.Create(Guid.Parse("8aa2a9f7-5e44-4ec1-bd46-0d9cb71bdc79"));

        Assert.NotEqual(first, second);
    }
}

public sealed class RewardGrantIdTests
{
    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = RewardGrantId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var id = RewardGrantId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => RewardGrantId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        Assert.Equal(RewardGrantId.Create(value), RewardGrantId.Create(value));
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = RewardGrantId.Create(Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06"));
        var second = RewardGrantId.Create(Guid.Parse("8aa2a9f7-5e44-4ec1-bd46-0d9cb71bdc79"));

        Assert.NotEqual(first, second);
    }
}

public sealed class EconomyBalanceRewardDefinitionTests
{
    [Fact]
    public void Create_CarriesIdAndPositiveAmount()
    {
        var id = RewardDefinitionId.New();

        var definition = EconomyBalanceRewardDefinition.Create(id, 42);

        Assert.Equal(id, definition.Id);
        Assert.Equal(42, definition.Amount);
    }

    [Fact]
    public void Create_RejectsZeroAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EconomyBalanceRewardDefinition.Create(RewardDefinitionId.New(), 0));
    }

    [Fact]
    public void Create_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EconomyBalanceRewardDefinition.Create(RewardDefinitionId.New(), -1));
    }

    [Fact]
    public void Create_AcceptsLongMaxValue()
    {
        var definition = EconomyBalanceRewardDefinition.Create(
            RewardDefinitionId.New(),
            long.MaxValue);

        Assert.Equal(long.MaxValue, definition.Amount);
    }

    [Fact]
    public void Create_RejectsAnEmptyDefinitionId()
    {
        Assert.Throws<ArgumentException>(() =>
            EconomyBalanceRewardDefinition.Create(default, 1));
    }
}

public sealed class RewardPackageTests
{
    [Fact]
    public void Create_CarriesPackageIdAndOneDefinition()
    {
        var packageId = RewardPackageId.New();
        var definitionId = RewardDefinitionId.New();

        var package = RewardPackage.Create(packageId, [definitionId]);

        Assert.Equal(packageId, package.Id);
        Assert.Equal([definitionId], package.RewardDefinitionIds);
    }

    [Fact]
    public void Create_AllowsSeveralDifferentDefinitions()
    {
        var first = RewardDefinitionId.New();
        var second = RewardDefinitionId.New();

        var package = RewardPackage.Create(RewardPackageId.New(), [first, second]);

        Assert.Equal([first, second], package.RewardDefinitionIds);
    }

    [Fact]
    public void Create_RejectsAnEmptyPackage()
    {
        Assert.Throws<ArgumentException>(() =>
            RewardPackage.Create(RewardPackageId.New(), Array.Empty<RewardDefinitionId>()));
    }

    [Fact]
    public void Create_RejectsAnEmptyPackageId()
    {
        Assert.Throws<ArgumentException>(() =>
            RewardPackage.Create(default, [RewardDefinitionId.New()]));
    }

    [Fact]
    public void Create_RejectsAnEmptyDefinitionId()
    {
        Assert.Throws<ArgumentException>(() =>
            RewardPackage.Create(RewardPackageId.New(), [default]));
    }

    [Fact]
    public void Create_RejectsDuplicateDefinitionIds()
    {
        var definitionId = RewardDefinitionId.New();

        Assert.Throws<ArgumentException>(() =>
            RewardPackage.Create(RewardPackageId.New(), [definitionId, definitionId]));
    }

    [Fact]
    public void ExposedCollectionCannotMutateInternalState()
    {
        var first = RewardDefinitionId.New();
        var second = RewardDefinitionId.New();
        var input = new[] { first };
        var package = RewardPackage.Create(RewardPackageId.New(), input);

        input[0] = second;

        Assert.Equal(first, package.RewardDefinitionIds[0]);
        var exposedList = Assert.IsAssignableFrom<IList<RewardDefinitionId>>(package.RewardDefinitionIds);
        Assert.Throws<NotSupportedException>(() => exposedList.Add(second));
        Assert.Equal([first], package.RewardDefinitionIds);
    }
}

public sealed class RewardSourceTests
{
    [Fact]
    public void Create_CarriesSourceTypeAndSourceId()
    {
        var source = RewardSource.Create("daily", "2026-08-30");

        Assert.Equal("daily", source.SourceType);
        Assert.Equal("2026-08-30", source.SourceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_RejectsAnInvalidSourceType(string? sourceType)
    {
        Assert.Throws<ArgumentException>(() => RewardSource.Create(sourceType, "source-id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_RejectsAnInvalidSourceId(string? sourceId)
    {
        Assert.Throws<ArgumentException>(() => RewardSource.Create("daily", sourceId));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var first = RewardSource.Create("daily", "2026-08-30");
        var second = RewardSource.Create("daily", "2026-08-30");

        Assert.Equal(first, second);
    }
}

public sealed class RewardGrantTests
{
    [Fact]
    public void Create_CarriesAllGrantFields()
    {
        var grantId = RewardGrantId.New();
        var communityIdentityId = CommunityIdentityId.New();
        var definitionId = RewardDefinitionId.New();
        var source = RewardSource.Create("daily", "2026-08-30");

        var grant = RewardGrant.Create(
            grantId,
            communityIdentityId,
            definitionId,
            source);

        Assert.Equal(grantId, grant.Id);
        Assert.Equal(communityIdentityId, grant.CommunityIdentityId);
        Assert.Equal(definitionId, grant.RewardDefinitionId);
        Assert.Equal(source, grant.Source);
    }

    [Fact]
    public void Create_RejectsAnEmptyGrantId()
    {
        Assert.Throws<ArgumentException>(() =>
            RewardGrant.Create(
                default,
                CommunityIdentityId.New(),
                RewardDefinitionId.New(),
                RewardSource.Create("daily", "2026-08-30")));
    }

    [Fact]
    public void Create_RejectsAnEmptyCommunityIdentityId()
    {
        Assert.Throws<ArgumentException>(() =>
            RewardGrant.Create(
                RewardGrantId.New(),
                default,
                RewardDefinitionId.New(),
                RewardSource.Create("daily", "2026-08-30")));
    }

    [Fact]
    public void Create_RejectsAnEmptyDefinitionId()
    {
        Assert.Throws<ArgumentException>(() =>
            RewardGrant.Create(
                RewardGrantId.New(),
                CommunityIdentityId.New(),
                default,
                RewardSource.Create("daily", "2026-08-30")));
    }

    [Fact]
    public void Create_RejectsANullSource()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RewardGrant.Create(
                RewardGrantId.New(),
                CommunityIdentityId.New(),
                RewardDefinitionId.New(),
                null));
    }
}
