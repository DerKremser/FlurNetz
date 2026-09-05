using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Rewards.Application;
using FlurNetz.Modules.Rewards.Domain;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Administration.Access")]
public sealed class CatalogModel(
    ListAchievementDefinitions achievements,
    ListTitleDefinitions titles,
    IRewardCatalogStore rewards,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<AchievementDefinition> AchievementDefinitions { get; private set; } = [];
    public IReadOnlyList<TitleDefinition> TitleDefinitions { get; private set; } = [];
    public IReadOnlyList<RewardDefinition> RewardDefinitions { get; private set; } = [];
    public IReadOnlyList<RewardPackage> RewardPackages { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken token)
    {
        var achievementTask = ReadAsync(() => achievements.ExecuteAsync(token), token);
        var titleTask = ReadAsync(() => titles.ExecuteAsync(token), token);
        var rewardTask = ReadAsync(() => rewards.ListDefinitionsAsync(token), token);
        var packageTask = ReadAsync(() => rewards.ListPackagesAsync(token), token);
        await Task.WhenAll(achievementTask, titleTask, rewardTask, packageTask).ConfigureAwait(false);
        AchievementDefinitions = achievementTask.Result ?? [];
        TitleDefinitions = titleTask.Result ?? [];
        RewardDefinitions = rewardTask.Result ?? [];
        RewardPackages = packageTask.Result ?? [];
        if (achievementTask.IsFaulted || titleTask.IsFaulted || rewardTask.IsFaulted || packageTask.IsFaulted)
        {
            Error = localizer["Error_CatalogUnavailable"].Value;
        }
    }

    private static async Task<T?> ReadAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return default; }
    }
}
