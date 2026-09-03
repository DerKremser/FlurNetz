using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin;

[AllowAnonymous]
public sealed class ForbiddenModel : PageModel
{
}
