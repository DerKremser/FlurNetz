using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Api.Administration;

public static class AdminBootstrapConfigurationReader
{
    public static bool TryRead(IConfiguration configuration, out AdminBootstrapConfiguration? result)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var identityText = configuration["Administration:Bootstrap:CommunityIdentityId"];
        var loginName = configuration["Administration:Bootstrap:LoginName"];
        var password = configuration["Administration:Bootstrap:InitialPassword"];
        var configured = !string.IsNullOrWhiteSpace(identityText)
            || !string.IsNullOrWhiteSpace(loginName)
            || !string.IsNullOrEmpty(password);
        if (!configured)
        {
            result = null;
            return false;
        }

        if (!Guid.TryParse(identityText, out var identity) || identity == Guid.Empty
            || string.IsNullOrWhiteSpace(loginName)
            || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "Administration-Bootstrap muss CommunityIdentityId, LoginName und InitialPassword vollständig enthalten.");
        }

        result = new AdminBootstrapConfiguration(
            CommunityIdentityId.Create(identity),
            loginName,
            password);
        return true;
    }
}
