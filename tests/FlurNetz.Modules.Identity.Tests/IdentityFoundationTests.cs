using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Identity.Domain;

namespace FlurNetz.Modules.Identity.Tests;

public sealed class CommunityIdentityIdTests
{
    [Fact]
    public void Create_AcceptsNonEmptyGuid()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var id = CommunityIdentityId.Create(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => CommunityIdentityId.Create(Guid.Empty));
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var value = Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06");

        var first = CommunityIdentityId.Create(value);
        var second = CommunityIdentityId.Create(value);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var first = CommunityIdentityId.Create(Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06"));
        var second = CommunityIdentityId.Create(Guid.Parse("8aa2a9f7-5e44-4ec1-bd46-0d9cb71bdc79"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void New_CreatesNonEmptyValue()
    {
        var id = CommunityIdentityId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }
}

public sealed class CommunityIdentityTests
{
    [Fact]
    public void Create_AcceptsValidIdentityId()
    {
        var id = CommunityIdentityId.New();

        var identity = CommunityIdentity.Create(id);

        Assert.NotNull(identity);
    }

    [Fact]
    public void Create_CarriesTheProvidedId()
    {
        var id = CommunityIdentityId.Create(Guid.Parse("4c3d1c3e-9b8e-4b5e-9f6c-7d4e8c2f1a06"));

        var identity = CommunityIdentity.Create(id);

        Assert.Equal(id, identity.Id);
    }

    [Fact]
    public void Create_RejectsAnInvalidIdentityId()
    {
        Assert.Throws<ArgumentException>(() => CommunityIdentity.Create(default));
    }

    [Fact]
    public void Id_IsExposedWithoutASetter()
    {
        var property = typeof(CommunityIdentity).GetProperty(nameof(CommunityIdentity.Id));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }
}
