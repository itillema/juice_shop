namespace JuiceShop.Automation.Definition.TestData;

/// <summary>A set of credentials for a Juice Shop account.</summary>
/// <param name="Email">Full email address, including domain.</param>
/// <param name="Password">Plain-text password.</param>
/// <param name="Description">Human-readable role, used in log and report output.</param>
public sealed record Credentials(string Email, string Password, string Description)
{
    public override string ToString() => $"{Description} <{Email}>";
}

/// <summary>Accounts seeded into Juice Shop at container start.</summary>
/// <remarks>
/// From <c>data/static/users.yml</c> in the image, so they hold only within the pinned digest, which is why they live in one file and docker-compose.yml pins by digest.
/// </remarks>
public static class TestUsers
{
    /// <summary>Administrator. Solves nothing by itself, but has the admin role.</summary>
    public static Credentials Admin { get; } =
        new("admin@juice-sh.op", "admin123", "Administrator");

    /// <summary>Ordinary customer. The default for journeys needing a pre-existing account.</summary>
    public static Credentials Customer { get; } =
        new("jim@juice-sh.op", "ncc-1701", "Customer");

    /// <summary>A second customer, for tests that need two distinct real accounts.</summary>
    public static Credentials SecondaryCustomer { get; } =
        new("bender@juice-sh.op", "OhG0dPlease1nsertLiquor!", "Secondary customer");

    /// <summary>Credentials that are guaranteed not to authenticate.</summary>
    public static Credentials Invalid { get; } =
        new("not-a-real-user@juice-sh.op", "definitely-not-the-password", "Unknown account");
}
