using System.Security.Claims;

namespace FrancProject.Extensions;

/// <summary>
/// Helpers for reading the caller's identity from the validated JWT.
/// <para>
/// Endpoints must resolve the acting user from these claims rather than from a
/// client-supplied <c>userId</c>, otherwise any authenticated caller can read or
/// mutate another user's data.
/// </para>
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public const string AdminRole = "Admin";

    /// <summary>Id of the authenticated caller.</summary>
    /// <exception cref="UnauthorizedAccessException">
    /// The token carries no usable <see cref="ClaimTypes.NameIdentifier"/> claim.
    /// </exception>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("The token does not contain a valid user id.");

        return userId;
    }

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(AdminRole);

    /// <summary>
    /// Resolves the user whose data an endpoint should operate on.
    /// Callers may only reach their own records; administrators may target anyone
    /// by passing <paramref name="requestedUserId"/> explicitly.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">
    /// A non-administrator requested a user other than themselves.
    /// </exception>
    public static int ResolveAccessibleUserId(this ClaimsPrincipal user, int? requestedUserId)
    {
        var callerId = user.GetUserId();

        if (requestedUserId is null or 0 || requestedUserId == callerId)
            return callerId;

        if (!user.IsAdmin())
            throw new UnauthorizedAccessException("You are not allowed to access another user's data.");

        return requestedUserId.Value;
    }
}
