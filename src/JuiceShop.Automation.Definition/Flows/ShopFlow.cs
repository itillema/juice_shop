using JuiceShop.Automation.Definition.Pages;
using JuiceShop.Automation.Definition.TestData;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Driver;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Definition.Flows;

/// <summary>The business-action facade that test cases are written against.</summary>
/// <remarks>
/// A journey in the vocabulary of the shop, not the browser. Page objects are built here from the session rather than resolved, which keeps them internal and out of a test's reach. See docs/adr/0003.
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

    /// <summary>Signs in as an existing user and waits for the session to be live.</summary>
    /// <remarks>Use <see cref="AttemptLoginAsync"/> when rejection is expected; this one times out.</remarks>
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
