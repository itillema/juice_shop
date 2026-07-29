using Bogus;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Utility.TestData;

/// <summary>Data needed to register a new Juice Shop account.</summary>
/// <param name="Email">Unique email address.</param>
/// <param name="Password">Password meeting Juice Shop's (minimal) policy.</param>
/// <param name="SecurityAnswer">Answer to the selected security question.</param>
public sealed record RegistrationData(string Email, string Password, string SecurityAnswer);

/// <summary>
/// Generates unique account data for tests that need their own user.
/// </summary>
/// <remarks>
/// <para>
/// This is the reason a test suite can run twice against the same container without a reset:
/// rather than sharing a seeded account and cleaning up after itself, each fixture that needs an
/// account registers a fresh one. Juice Shop re-seeds its whole database on restart, so accumulated
/// test accounts are discarded the moment the container cycles.
/// </para>
/// <para>
/// Per ISTQB CTAL-TAE 2016 §3.1.2, deriving test data is a Test Generation concern rather than a
/// utility one. It lives in this project because the alternative — a fifth project holding two
/// classes — trades a real cost for a naming technicality. See docs/architecture.md.
/// </para>
/// </remarks>
public sealed class RegistrationDataFactory
{
    private readonly Faker<RegistrationData> _faker;

    // Bogus keeps mutable pseudo-random state inside the Faker, so Generate() is not thread-safe.
    // This factory is a singleton and NUnit runs fixtures in parallel, so without this lock two
    // tests can generate at the same time and corrupt each other's data.
    private readonly Lock _generationLock = new();

    public RegistrationDataFactory(IOptions<AutomationSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var testData = settings.Value.TestData;

        _faker = new Faker<RegistrationData>()
            .UseSeed(testData.Seed)
            .CustomInstantiator(faker => new RegistrationData(
                Email: BuildUniqueEmail(testData.EmailDomain),
                Password: faker.Internet.Password(length: 12, prefix: "Aa1!"),
                SecurityAnswer: faker.Address.City()));
    }

    /// <summary>Generates one set of registration data with a globally unique email address.</summary>
    public RegistrationData Create()
    {
        lock (_generationLock)
        {
            return _faker.Generate();
        }
    }

    /// <summary>
    /// Builds a collision-free address. The Bogus seed is fixed so that passwords and security
    /// answers are reproducible across runs, which means the email cannot also come from the seeded
    /// generator — two runs would produce the same address and the second registration would fail
    /// with a uniqueness error. A GUID gives uniqueness without giving up reproducibility of
    /// everything else.
    /// </summary>
    private static string BuildUniqueEmail(string domain) =>
        $"e2e-{Guid.NewGuid():N}@{domain}";
}
