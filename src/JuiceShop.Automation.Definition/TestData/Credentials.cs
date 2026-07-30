namespace JuiceShop.Automation.Definition.TestData;

/// <summary>A set of credentials for a Juice Shop account.</summary>
/// <param name="Email">Full email address, including domain.</param>
/// <param name="Password">Plain-text password.</param>
/// <param name="Description">Human-readable role, used in log and report output.</param>
public sealed record Credentials(string Email, string Password, string Description)
{
    public override string ToString() => $"{Description} <{Email}>";
}

/// <summary>
/// Accounts seeded into Juice Shop at container start.
/// </summary>
/// <remarks>
/// <para>
/// These come from <c>data/static/users.yml</c> in the Juice Shop image, where the email is stored
/// as a bare local part and the domain (<c>juice-sh.op</c>) is appended during seeding. The seed is
/// deterministic because <c>application.numberOfRandomFakeUsers</c> is 0 by default.
/// </para>
/// <para>
/// Upstream's own Cypress suite hardcodes several of these, so they are effectively contract — but
/// only within a pinned image tag, which is the reason docker-compose.yml pins by digest. Keeping
/// them in one file means a version bump is a single-file change rather than a search-and-replace.
/// </para>
/// </remarks>
public static class TestUsers
{
    /// <summary>Administrator. Solves nothing by itself, but has the admin role.</summary>
    public static Credentials Admin { get; } =
        new("admin@juice-sh.op", "admin123", "Administrator");

    /// <summary>
    /// Ordinary customer with a seeded wallet balance of 100. The default choice for shopping
    /// journeys that need a pre-existing account.
    /// </summary>
    public static Credentials Customer { get; } =
        new("jim@juice-sh.op", "ncc-1701", "Customer");

    /// <summary>A second customer, for tests that need two distinct real accounts.</summary>
    public static Credentials SecondaryCustomer { get; } =
        new("bender@juice-sh.op", "OhG0dPlease1nsertLiquor!", "Secondary customer");

    /// <summary>Credentials that are guaranteed not to authenticate.</summary>
    public static Credentials Invalid { get; } =
        new("not-a-real-user@juice-sh.op", "definitely-not-the-password", "Unknown account");
}
