using Bogus;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Definition.TestData;

/// <summary>Data needed to register a new Juice Shop account.</summary>
/// <param name="Email">Unique email address.</param>
/// <param name="Password">Password meeting Juice Shop's (minimal) policy.</param>
/// <param name="SecurityAnswer">Answer to the selected security question.</param>
public sealed record RegistrationData(string Email, string Password, string SecurityAnswer);

/// <summary>Generates unique account data for tests that need their own user.</summary>
/// <remarks>
/// Registering fresh beats sharing a seeded account: it is order-independent and parallel-safe. See docs/architecture.md — "Test isolation".
/// </remarks>
public sealed class RegistrationDataFactory
{
    private readonly Faker<RegistrationData> _faker;

    // Faker holds mutable state, so Generate() is not thread-safe. Singleton + parallel fixtures.
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

    /// <summary>GUID, not Bogus: the fixed seed would repeat the address and fail the second run.</summary>
    private static string BuildUniqueEmail(string domain) =>
        $"e2e-{Guid.NewGuid():N}@{domain}";
}
