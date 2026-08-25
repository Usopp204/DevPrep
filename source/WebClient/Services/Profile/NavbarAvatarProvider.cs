namespace WebClient.Services.Profile;

/// <summary>
/// Supplies the signed-in user's avatar URL to the navbar partials, which render on nearly every
/// authenticated page.
/// </summary>
public interface INavbarAvatarProvider
{
    /// <summary>
    /// The current user's avatar URL, or null when there is none, nobody is signed in, or the
    /// lookup could not be completed. Never throws — see the implementation for why that matters.
    /// </summary>
    Task<string?> GetAvatarUrlAsync();

    /// <summary>
    /// Drops the cached value so the next page render looks it up again. Called after a
    /// successful avatar upload.
    /// </summary>
    void Invalidate();
}

/// <summary>
/// Session-cached, lazily populated. The alternative designs were rejected for reasons worth
/// keeping: calling the profile API on every render would add a blocking round trip to every
/// authenticated page; putting the URL in a cookie would make it user-editable (user_email and
/// user_role are deliberately not HttpOnly) and add bytes to every request; a JWT claim would
/// need AuthAPI, which this project does not own; and fetching from JS after render would
/// reproduce the very symptom this feature was asked to fix — the initial showing first and the
/// picture swapping in a moment later.
///
/// Correctness rests on there being exactly ONE writer of a user's avatar: their own
/// ProfileController.Edit POST, which calls <see cref="Invalidate"/>. No admin or manager flow
/// changes another user's avatar, so there is no path by which this cache can silently go stale.
/// </summary>
public sealed class NavbarAvatarProvider(
    IHttpContextAccessor httpContextAccessor,
    IProfileService profileService,
    ILogger<NavbarAvatarProvider> logger) : INavbarAvatarProvider
{
    private const string SessionKey = "navbar_avatar_url";

    // Empty string = "looked up, this user has no avatar". Storing the miss is the whole point:
    // keying only on presence/absence would re-fetch on every single page for every user who has
    // never uploaded one, which is exactly the per-page cost this design exists to avoid.
    private const string NoAvatarSentinel = "";

    // Upper bound on how long a page render will wait for this. HttpClient's own timeout is 95s
    // (see ServiceCollectionExtensions' timeout ladder) — correct for a user-initiated action,
    // far too long for something on the critical path of every page. If UserAPI is unreachable
    // and hanging rather than refusing, that 95s would stall the whole site, not just the avatar.
    private static readonly TimeSpan LookupBudget = TimeSpan.FromSeconds(2);

    public async Task<string?> GetAvatarUrlAsync()
    {
        // Everything is inside the try, including reading Session and the cookie: this runs on
        // nearly every authenticated page, so any exception escaping here would take down whole
        // pages rather than just degrade one icon. Every failure path returns null, and null
        // simply means the caller draws its fallback character.
        try
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
                return null;

            // Signed out: nothing to look up and, more importantly, no call to make.
            if (string.IsNullOrEmpty(context.Request.Cookies["access_token"]))
                return null;

            // Admin and Manager navbars render the letter fallback ("A"/"M") by design and never
            // show an uploaded picture, so fetching one for them would be a profile call per
            // session for a value nothing renders. Checked here rather than in the partials so it
            // holds for every call site — including the candidate menus, which an admin can also
            // land on. Same cookie and comparison the partials already use for accountIsAdmin.
            //
            // user_role is not HttpOnly, so a user can edit it. The only thing that buys them is
            // suppressing their OWN avatar; this decides presentation, never access.
            var role = context.Request.Cookies["user_role"];
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var session = context.Session;
            var cached = session.GetString(SessionKey);
            if (cached is not null)
                return cached.Length == 0 ? null : cached;

            // Bounded wait. The abandoned task completes harmlessly in the background if it is
            // just slow; we only decline to keep the page waiting on it.
            var lookup = profileService.GetMyProfileAsync();
            if (await Task.WhenAny(lookup, Task.Delay(LookupBudget)) != lookup)
            {
                logger.LogWarning(
                    "Navbar avatar lookup exceeded {Budget}s; rendering the initial instead.",
                    LookupBudget.TotalSeconds);
                return null;
            }

            var result = await lookup;

            // NOT cached on failure, deliberately: a failed lookup must not turn into "this user
            // has no avatar" for the rest of their session. The cost of retrying is one bounded
            // call per page while UserAPI is unwell, and it heals itself the moment it recovers.
            if (!result.Success || result.Profile is null)
                return null;

            var avatarUrl = result.Profile.AvatarUrl;
            var hasAvatar = !string.IsNullOrWhiteSpace(avatarUrl);
            session.SetString(SessionKey, hasAvatar ? avatarUrl! : NoAvatarSentinel);
            return hasAvatar ? avatarUrl : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Navbar avatar lookup failed; rendering the initial instead.");
            return null;
        }
    }

    public void Invalidate()
    {
        try
        {
            httpContextAccessor.HttpContext?.Session.Remove(SessionKey);
        }
        catch (Exception ex)
        {
            // Worst case the navbar shows the previous picture until the session ends. Not worth
            // failing the upload the user just completed successfully.
            logger.LogWarning(ex, "Could not invalidate the cached navbar avatar.");
        }
    }
}
