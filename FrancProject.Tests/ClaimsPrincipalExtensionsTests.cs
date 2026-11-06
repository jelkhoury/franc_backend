using System.Security.Claims;
using FrancProject.Extensions;

namespace FrancProject.Tests;

/// <summary>
/// These cover the access rule that replaced the IDOR-prone pattern of reading
/// <c>userId</c> straight from the query string. The invariant under test: a caller
/// reaches their own records, an administrator reaches anyone's, and nobody else
/// reaches another user's by asking for them.
/// </summary>
public class ClaimsPrincipalExtensionsTests
{
    private const int CallerId = 42;
    private const int OtherUserId = 999;

    private static ClaimsPrincipal Caller(int? userId = CallerId, string? role = "User")
    {
        var claims = new List<Claim>();

        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // The authentication type must be non-null for IsInRole to evaluate.
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static ClaimsPrincipal Admin(int userId = 1) => Caller(userId, "Admin");

    // ---------------------------------------------------------------
    // GetUserId
    // ---------------------------------------------------------------

    [Fact]
    public void GetUserId_returns_the_id_from_the_token()
    {
        Assert.Equal(CallerId, Caller().GetUserId());
    }

    [Fact]
    public void GetUserId_rejects_a_token_with_no_user_id()
    {
        // Regression guard: the previous int.Parse(...!) threw ArgumentNullException,
        // which the global handler surfaced as 500. A malformed token is a 401.
        Assert.Throws<UnauthorizedAccessException>(() => Caller(userId: null).GetUserId());
    }

    [Fact]
    public void GetUserId_rejects_a_non_numeric_user_id()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-number") }, "TestAuth"));

        Assert.Throws<UnauthorizedAccessException>(() => principal.GetUserId());
    }

    // ---------------------------------------------------------------
    // IsAdmin
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("User", false)]
    [InlineData("admin", false)]   // role comparison is case-sensitive
    public void IsAdmin_recognises_only_the_exact_Admin_role(string role, bool expected)
    {
        Assert.Equal(expected, Caller(role: role).IsAdmin());
    }

    [Fact]
    public void IsAdmin_is_false_when_the_token_carries_no_role()
    {
        Assert.False(Caller(role: null).IsAdmin());
    }

    // ---------------------------------------------------------------
    // ResolveAccessibleUserId — the access rule itself
    // ---------------------------------------------------------------

    [Fact]
    public void Omitting_the_user_id_resolves_to_the_caller()
    {
        Assert.Equal(CallerId, Caller().ResolveAccessibleUserId(null));
    }

    [Fact]
    public void Requesting_your_own_id_resolves_to_you()
    {
        Assert.Equal(CallerId, Caller().ResolveAccessibleUserId(CallerId));
    }

    [Fact]
    public void Zero_is_treated_as_unspecified_and_resolves_to_the_caller()
    {
        // Lets a client that still posts a default-valued userId degrade to "me"
        // rather than fail. Identity columns start at 1, so 0 is never a real user.
        Assert.Equal(CallerId, Caller().ResolveAccessibleUserId(0));
    }

    [Fact]
    public void A_user_cannot_reach_another_users_records()
    {
        // This is the IDOR that let any account read every other account's data
        // by incrementing a number in the query string.
        Assert.Throws<UnauthorizedAccessException>(
            () => Caller().ResolveAccessibleUserId(OtherUserId));
    }

    [Fact]
    public void An_admin_can_reach_another_users_records()
    {
        // The admin dashboard depends on this, so the IDOR fix must not block it.
        Assert.Equal(OtherUserId, Admin().ResolveAccessibleUserId(OtherUserId));
    }

    [Fact]
    public void An_unauthenticated_principal_is_rejected_before_any_id_is_returned()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => new ClaimsPrincipal(new ClaimsIdentity()).ResolveAccessibleUserId(CallerId));
    }

    [Fact]
    public void A_role_claim_alone_does_not_grant_access_without_a_user_id()
    {
        // An Admin role on a token with no usable NameIdentifier must still fail,
        // rather than falling through to the requested id.
        Assert.Throws<UnauthorizedAccessException>(
            () => Caller(userId: null, role: "Admin").ResolveAccessibleUserId(OtherUserId));
    }
}
