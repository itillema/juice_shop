using JuiceShop.Automation.Definition.TestData;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>A product as displayed in the catalogue.</summary>
internal sealed record ProductSummary(string Name, decimal Price);

/// <summary>A line in the shopping basket.</summary>
internal sealed record BasketLine(string ProductName, int Quantity);

/// <summary>
/// The login page at <c>/#/login</c>.
/// </summary>
/// <remarks>
/// Each page contract exposes both actions and its own assertions. Keeping the assertions here —
/// rather than handing a locator upward for the test to assert on — is what confines Playwright's
/// auto-retrying web-first assertions to the layer that owns the locators. A test that reads an
/// element's text and then asserts on the string has silently opted out of retrying, and is the
/// single most common cause of flaky .NET Playwright suites.
/// </remarks>
internal interface ILoginPage
{
    /// <summary>Navigates directly to the login page.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Fills the credential fields and submits.</summary>
    Task SignInAsync(Credentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until a successful sign-in has actually taken effect in the application.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SignInAsync"/> because the two are needed independently: the
    /// negative tests submit credentials and expect the app to stay put, so they must not wait for
    /// an authentication that is never going to arrive.
    /// </remarks>
    Task WaitForSignInToCompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>Follows the "Not yet a customer?" link to registration.</summary>
    Task GoToRegistrationAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts that an authentication error is displayed.</summary>
    Task ShouldShowInvalidCredentialsErrorAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts that the login form is visible and ready for input.</summary>
    Task ShouldBeDisplayedAsync(CancellationToken cancellationToken = default);
}

/// <summary>The registration page at <c>/#/register</c>.</summary>
internal interface IRegistrationPage
{
    /// <summary>Navigates directly to the registration page.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Completes and submits the registration form.</summary>
    Task RegisterAsync(RegistrationData registration, CancellationToken cancellationToken = default);

    /// <summary>Asserts the registration form is visible.</summary>
    Task ShouldBeDisplayedAsync(CancellationToken cancellationToken = default);
}

/// <summary>The persistent navigation bar, present on every route.</summary>
internal interface INavigationBar
{
    /// <summary>Opens the account menu and signs out.</summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens the account menu and navigates to login.</summary>
    Task GoToLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens the basket.</summary>
    Task GoToBasketAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts that a user is signed in.</summary>
    Task ShouldShowSignedInAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts that no user is signed in.</summary>
    Task ShouldShowSignedOutAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts the basket badge shows the given number of items.</summary>
    Task ShouldShowBasketItemCountAsync(int expected, CancellationToken cancellationToken = default);
}

/// <summary>The product catalogue at <c>/#/search</c>.</summary>
internal interface IProductCatalogPage
{
    /// <summary>Navigates to the catalogue.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs a search and waits for the result set to settle.</summary>
    Task SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>Adds the first visible search result to the basket.</summary>
    /// <returns>The product that was added.</returns>
    Task<ProductSummary> AddFirstResultToBasketAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a specific product, by exact display name, to the basket.</summary>
    Task AddToBasketAsync(string productName, CancellationToken cancellationToken = default);

    /// <summary>Reads the currently displayed products.</summary>
    Task<IReadOnlyList<ProductSummary>> GetVisibleProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts at least one result is displayed.</summary>
    Task ShouldShowResultsAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts that no results are displayed.</summary>
    Task ShouldShowNoResultsAsync(CancellationToken cancellationToken = default);

    /// <summary>Asserts a product with the given name is displayed.</summary>
    Task ShouldDisplayProductAsync(string productName, CancellationToken cancellationToken = default);
}

/// <summary>The shopping basket at <c>/#/basket</c>.</summary>
internal interface IBasketPage
{
    /// <summary>Navigates to the basket.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the current basket contents.</summary>
    Task<IReadOnlyList<BasketLine>> GetLinesAsync(CancellationToken cancellationToken = default);

    /// <summary>Increases the quantity of a basket line by one.</summary>
    Task IncreaseQuantityAsync(string productName, CancellationToken cancellationToken = default);

    /// <summary>Decreases the quantity of a basket line by one.</summary>
    Task DecreaseQuantityAsync(string productName, CancellationToken cancellationToken = default);

    /// <summary>Removes a line from the basket.</summary>
    Task RemoveAsync(string productName, CancellationToken cancellationToken = default);

    /// <summary>Asserts the basket contains a line for the given product.</summary>
    Task ShouldContainAsync(string productName, CancellationToken cancellationToken = default);

    /// <summary>Asserts the basket does not contain a line for the given product.</summary>
    Task ShouldNotContainAsync(string productName, CancellationToken cancellationToken = default);

    /// <summary>Asserts the quantity shown against a product.</summary>
    Task ShouldShowQuantityAsync(string productName, int expected, CancellationToken cancellationToken = default);

    /// <summary>Asserts the basket has the given number of distinct lines.</summary>
    Task ShouldHaveLineCountAsync(int expected, CancellationToken cancellationToken = default);
}
