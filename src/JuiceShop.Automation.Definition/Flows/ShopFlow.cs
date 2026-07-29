using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Utility.TestData;

namespace JuiceShop.Automation.Definition.Flows;

/// <summary>
/// The business-action facade that test cases are written against.
/// </summary>
/// <remarks>
/// <para>
/// This is ISTQB CTAL-TAE v2.0 §3.1.5's <em>flow model pattern</em>: "an additional facade over the
/// page object models, which stores all the user actions that interact with the page objects".
/// The double facade is what lets a test read as a sequence of business intentions while every
/// detail of how those intentions reach the browser stays one layer down.
/// </para>
/// <para>
/// The action methods return <see cref="ShopFlow"/> so journeys chain. Assertions are reached
/// through the facades on this type rather than by exposing the Adaptation page contracts directly,
/// so that a test case never names a type from the Adaptation layer.
/// </para>
/// </remarks>
public sealed class ShopFlow
{
    private readonly IBrowserSession _session;

    public ShopFlow(IBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        Basket = new BasketAssertions(session.Basket);
        Catalog = new CatalogAssertions(session.Catalog);
        Navigation = new NavigationAssertions(session.Navigation);
        LoginPage = new LoginAssertions(session.Login);
    }

    /// <summary>Assertions and actions against the shopping basket.</summary>
    public BasketAssertions Basket { get; }

    /// <summary>Assertions about the product catalogue.</summary>
    public CatalogAssertions Catalog { get; }

    /// <summary>Assertions about session state, as reflected in the navigation bar.</summary>
    public NavigationAssertions Navigation { get; }

    /// <summary>Assertions about the login page.</summary>
    public LoginAssertions LoginPage { get; }

    /// <summary>
    /// Signs in as an existing user and waits for the session to be live.
    /// </summary>
    /// <remarks>
    /// Use <see cref="AttemptLoginAsync"/> when the sign-in is expected to be rejected — this one
    /// waits for authentication to take effect and will time out if it never does.
    /// </remarks>
    public async Task<ShopFlow> LoginAsAsync(Credentials credentials)
    {
        await _session.Login.OpenAsync();
        await _session.Login.SignInAsync(credentials);
        await _session.Login.WaitForSignInToCompleteAsync();
        return this;
    }

    /// <summary>Attempts a sign-in without expecting it to succeed.</summary>
    public async Task<ShopFlow> AttemptLoginAsync(Credentials credentials)
    {
        await _session.Login.OpenAsync();
        await _session.Login.SignInAsync(credentials);
        return this;
    }

    /// <summary>Registers a brand new account and then signs in with it.</summary>
    public async Task<ShopFlow> RegisterAndLoginAsync(RegistrationData registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await _session.Registration.OpenAsync();
        await _session.Registration.RegisterAsync(registration);

        await _session.Login.OpenAsync();
        await _session.Login.SignInAsync(
            new Credentials(registration.Email, registration.Password, "Newly registered customer"));
        await _session.Login.WaitForSignInToCompleteAsync();

        return this;
    }

    /// <summary>Signs the current user out.</summary>
    public async Task<ShopFlow> LogoutAsync()
    {
        await _session.Navigation.SignOutAsync();
        return this;
    }

    /// <summary>Searches the catalogue.</summary>
    public async Task<ShopFlow> SearchAsync(string searchTerm)
    {
        await _session.Catalog.OpenAsync();
        await _session.Catalog.SearchAsync(searchTerm);
        return this;
    }

    /// <summary>Adds the first search result to the basket.</summary>
    /// <returns>The product that was added, so the test can assert against it by name.</returns>
    public async Task<Product> AddFirstResultToBasketAsync()
    {
        var product = await _session.Catalog.AddFirstResultToBasketAsync();
        return new Product(product.Name, product.Price);
    }

    /// <summary>Adds a named product to the basket from the catalogue.</summary>
    public async Task<ShopFlow> AddToBasketAsync(string productName)
    {
        await _session.Catalog.AddToBasketAsync(productName);
        return this;
    }

    /// <summary>Opens the basket.</summary>
    public async Task<ShopFlow> OpenBasketAsync()
    {
        await _session.Basket.OpenAsync();
        return this;
    }
}
