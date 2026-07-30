using JuiceShop.Automation.Definition.Pages;
using JuiceShop.Automation.Definition.TestData;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Driver;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Definition.Flows;

/// <summary>
/// The business-action facade that test cases are written against.
/// </summary>
/// <remarks>
/// <para>
/// A flow is a user journey expressed once, in the vocabulary of the shop rather than of the
/// browser. It composes page objects, which own locators and atomic interactions; the double facade
/// is what lets a test read as a sequence of intentions while every detail of how those intentions
/// reach the browser stays one level down.
/// </para>
/// <para>
/// This type builds the page objects itself from the session's page rather than resolving them from
/// the container. Page objects are per-session and would need a scope the container has no way to
/// model, and keeping them out of the container is what allows them — and their contracts — to stay
/// internal to this assembly. That internal visibility is the mechanism that makes it impossible for
/// a test case to reach a locator.
/// </para>
/// </remarks>
public sealed class ShopFlow
{
    private readonly LoginPage _login;
    private readonly RegistrationPage _registration;
    private readonly NavigationBar _navigation;
    private readonly ProductCatalogPage _catalog;
    private readonly BasketPage _basket;

    /// <summary>Builds the flow facade over a browsing session.</summary>
    public ShopFlow(IBrowserSession session, IOptions<AutomationSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);

        var page = session.Page;
        var expectTimeout = settings.Value.Browser.ExpectTimeoutMilliseconds;

        _login = new LoginPage(page, expectTimeout);
        _registration = new RegistrationPage(page, expectTimeout);
        _navigation = new NavigationBar(page, expectTimeout);
        _catalog = new ProductCatalogPage(page, expectTimeout);
        _basket = new BasketPage(page, expectTimeout);

        Basket = new BasketAssertions(_basket);
        Catalog = new CatalogAssertions(_catalog);
        Navigation = new NavigationAssertions(_navigation);
        LoginPage = new LoginAssertions(_login);
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
        await _login.OpenAsync();
        await _login.SignInAsync(credentials);
        await _login.WaitForSignInToCompleteAsync();
        return this;
    }

    /// <summary>Attempts a sign-in without expecting it to succeed.</summary>
    public async Task<ShopFlow> AttemptLoginAsync(Credentials credentials)
    {
        await _login.OpenAsync();
        await _login.SignInAsync(credentials);
        return this;
    }

    /// <summary>Registers a brand new account and then signs in with it.</summary>
    public async Task<ShopFlow> RegisterAndLoginAsync(RegistrationData registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await _registration.OpenAsync();
        await _registration.RegisterAsync(registration);

        await _login.OpenAsync();
        await _login.SignInAsync(
            new Credentials(registration.Email, registration.Password, "Newly registered customer"));
        await _login.WaitForSignInToCompleteAsync();

        return this;
    }

    /// <summary>Signs the current user out.</summary>
    public async Task<ShopFlow> LogoutAsync()
    {
        await _navigation.SignOutAsync();
        return this;
    }

    /// <summary>Searches the catalogue.</summary>
    public async Task<ShopFlow> SearchAsync(string searchTerm)
    {
        await _catalog.OpenAsync();
        await _catalog.SearchAsync(searchTerm);
        return this;
    }

    /// <summary>Adds the first search result to the basket.</summary>
    /// <returns>The product that was added, so the test can assert against it by name.</returns>
    public async Task<Product> AddFirstResultToBasketAsync()
    {
        var product = await _catalog.AddFirstResultToBasketAsync();
        return new Product(product.Name, product.Price);
    }

    /// <summary>Adds a named product to the basket from the catalogue.</summary>
    public async Task<ShopFlow> AddToBasketAsync(string productName)
    {
        await _catalog.AddToBasketAsync(productName);
        return this;
    }

    /// <summary>Opens the basket.</summary>
    public async Task<ShopFlow> OpenBasketAsync()
    {
        await _basket.OpenAsync();
        return this;
    }
}
