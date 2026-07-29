using JuiceShop.Automation.Adaptation.Contracts.Pages;

namespace JuiceShop.Automation.Definition.Flows;

/// <summary>
/// Domain-language assertions about the shopping basket.
/// </summary>
/// <remarks>
/// <para>
/// These facades exist so that a test case never names a type from the Adaptation layer. An earlier
/// version of <see cref="ShopFlow"/> simply re-exposed the page contracts
/// (<c>public IBasketPage Basket =&gt; session.Basket</c>), which read fine but meant every fixture
/// carried a compile-time dependency on the adapters — the exact coupling ISTQB CTAL-TAE v2.0
/// §3.1.3 rules out with "no direct calls should be made to the core libraries from test scripts".
/// <c>ArchitectureTests</c> caught it.
/// </para>
/// <para>
/// The forwarding is deliberately thin. The auto-retrying assertion logic stays in the page objects,
/// where the locators are, because that is what keeps the retry semantics correct. What this layer
/// contributes is vocabulary: the test says what should be true of the shop, not which element it
/// should be read from. It is also the natural home for assertions that span more than one page.
/// </para>
/// </remarks>
public sealed class BasketAssertions
{
    private readonly IBasketPage _basket;

    internal BasketAssertions(IBasketPage basket) => _basket = basket;

    /// <summary>Asserts the basket has a line for the given product.</summary>
    public Task ShouldContainAsync(string productName) => _basket.ShouldContainAsync(productName);

    /// <summary>Asserts the basket has no line for the given product.</summary>
    public Task ShouldNotContainAsync(string productName) => _basket.ShouldNotContainAsync(productName);

    /// <summary>Asserts the quantity shown against a product.</summary>
    public Task ShouldShowQuantityAsync(string productName, int expected) =>
        _basket.ShouldShowQuantityAsync(productName, expected);

    /// <summary>Asserts the number of distinct lines in the basket.</summary>
    public Task ShouldHaveLineCountAsync(int expected) => _basket.ShouldHaveLineCountAsync(expected);

    /// <summary>Asserts the basket is empty.</summary>
    public Task ShouldBeEmptyAsync() => _basket.ShouldHaveLineCountAsync(0);

    /// <summary>Increases a line's quantity by one.</summary>
    public Task IncreaseQuantityAsync(string productName) => _basket.IncreaseQuantityAsync(productName);

    /// <summary>Decreases a line's quantity by one.</summary>
    public Task DecreaseQuantityAsync(string productName) => _basket.DecreaseQuantityAsync(productName);

    /// <summary>Removes a line from the basket.</summary>
    public Task RemoveAsync(string productName) => _basket.RemoveAsync(productName);
}

/// <summary>Domain-language assertions about the product catalogue.</summary>
public sealed class CatalogAssertions
{
    private readonly IProductCatalogPage _catalog;

    internal CatalogAssertions(IProductCatalogPage catalog) => _catalog = catalog;

    /// <summary>Asserts at least one product is displayed.</summary>
    public Task ShouldShowResultsAsync() => _catalog.ShouldShowResultsAsync();

    /// <summary>Asserts the empty state is displayed.</summary>
    public Task ShouldShowNoResultsAsync() => _catalog.ShouldShowNoResultsAsync();

    /// <summary>Asserts a named product is displayed.</summary>
    public Task ShouldDisplayProductAsync(string productName) => _catalog.ShouldDisplayProductAsync(productName);

    /// <summary>
    /// Reads the displayed products as plain records, for tests that need to assert on the data
    /// rather than on the rendering.
    /// </summary>
    public async Task<IReadOnlyList<Product>> GetVisibleProductsAsync()
    {
        var products = await _catalog.GetVisibleProductsAsync();
        return [.. products.Select(product => new Product(product.Name, product.Price))];
    }
}

/// <summary>Domain-language assertions about the navigation bar and session state.</summary>
public sealed class NavigationAssertions
{
    private readonly INavigationBar _navigation;

    internal NavigationAssertions(INavigationBar navigation) => _navigation = navigation;

    /// <summary>Asserts a user is signed in.</summary>
    public Task ShouldShowSignedInAsync() => _navigation.ShouldShowSignedInAsync();

    /// <summary>Asserts no user is signed in.</summary>
    public Task ShouldShowSignedOutAsync() => _navigation.ShouldShowSignedOutAsync();

    /// <summary>Asserts the basket badge shows the given item count.</summary>
    public Task ShouldShowBasketItemCountAsync(int expected) =>
        _navigation.ShouldShowBasketItemCountAsync(expected);
}

/// <summary>Domain-language assertions about the login page.</summary>
public sealed class LoginAssertions
{
    private readonly ILoginPage _login;

    internal LoginAssertions(ILoginPage login) => _login = login;

    /// <summary>Asserts an authentication failure is displayed.</summary>
    public Task ShouldShowInvalidCredentialsErrorAsync() => _login.ShouldShowInvalidCredentialsErrorAsync();

    /// <summary>Asserts the login form is ready for input.</summary>
    public Task ShouldBeDisplayedAsync() => _login.ShouldBeDisplayedAsync();
}

/// <summary>A product, as the test cases see it.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Price">Unit price.</param>
public sealed record Product(string Name, decimal Price);
