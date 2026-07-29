using System.Globalization;
using JuiceShop.Automation.Adaptation.Contracts.Pages;
using JuiceShop.Automation.Adaptation.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Adaptation.Pages;

/// <summary>Page object for the product catalogue and search at <c>/#/search</c>.</summary>
internal sealed class ProductCatalogPage : PageObjectBase, IProductCatalogPage
{
    public ProductCatalogPage(IPage page, int expectTimeoutMilliseconds)
        : base(page, expectTimeoutMilliseconds)
    {
    }

    /// <summary>The collapsed search control in the toolbar (an <c>app-mat-search-bar</c>).</summary>
    private ILocator SearchToggle => Page.Locator("#searchQuery button[aria-label='Open search']");

    private ILocator SearchInput => Page.Locator("#searchQuery input");

    private ILocator ProductCards => Page.Locator("app-product");

    private ILocator NoResultsText => Page.Locator(".noResultText").First;

    public Task OpenAsync(CancellationToken cancellationToken = default) =>
        NavigateAsync(JuiceShopRoutes.Search);

    public async Task SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        // The control renders as an icon button and only reveals its input once expanded — but it
        // stays expanded after a search. Expanding unconditionally would therefore work for the
        // first search in a session and hang on every subsequent one, because the toggle button is
        // gone. Checking first makes the method idempotent with respect to the control's state.
        if (!await SearchInput.IsVisibleAsync())
        {
            await Expect(SearchToggle).ToBeVisibleAsync(VisibleOptions);
            await SearchToggle.ClickAsync();
        }

        await Expect(SearchInput).ToBeVisibleAsync(VisibleOptions);
        await SearchInput.FillAsync(searchTerm);
        await SearchInput.PressAsync("Enter");

        // Angular replaces the result set in place rather than navigating, so there is no load
        // event to await. The query lands in the URL, and waiting for that is the signal that the
        // new search has actually been dispatched.
        await Page.WaitForURLAsync(
            url => url.Contains("/search", StringComparison.OrdinalIgnoreCase)
                && url.Contains('q', StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProductSummary> AddFirstResultToBasketAsync(CancellationToken cancellationToken = default)
    {
        var firstCard = ProductCards.First;
        await Expect(firstCard).ToBeVisibleAsync(VisibleOptions);

        var product = await ReadCardAsync(firstCard);
        await AddCardToBasketAsync(firstCard);

        return product;
    }

    public async Task AddToBasketAsync(string productName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        var card = ProductTile(productName).First;
        await Expect(card).ToBeVisibleAsync(VisibleOptions);
        await AddCardToBasketAsync(card);
    }

    public async Task<IReadOnlyList<ProductSummary>> GetVisibleProductsAsync(CancellationToken cancellationToken = default)
    {
        await Expect(ProductCards.First).ToBeVisibleAsync(VisibleOptions);

        var cards = await ProductCards.AllAsync();
        var products = new List<ProductSummary>(cards.Count);

        foreach (var card in cards)
        {
            products.Add(await ReadCardAsync(card));
        }

        return products;
    }

    public Task ShouldShowResultsAsync(CancellationToken cancellationToken = default) =>
        Expect(ProductCards.First).ToBeVisibleAsync(VisibleOptions);

    public async Task ShouldShowNoResultsAsync(CancellationToken cancellationToken = default)
    {
        // Asserted two ways because each alone is weak: the application renders an empty-state card
        // when nothing matches, so "no cards on the page" is not sufficient on its own, and the
        // message element exists in the DOM before it becomes relevant.
        await Expect(ProductCards).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
        await Expect(NoResultsText).ToBeVisibleAsync(VisibleOptions);
    }

    public Task ShouldDisplayProductAsync(string productName, CancellationToken cancellationToken = default) =>
        Expect(ProductTile(productName).First).ToBeVisibleAsync(VisibleOptions);

    /// <summary>
    /// Clicks a card's add button and waits for the basket write to actually complete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The click fires an asynchronous request and returns immediately. Navigating to the basket
    /// straight afterwards races that request: the badge in the toolbar updates optimistically on
    /// the client, so the UI looks correct, while the basket page fetches its contents and renders
    /// an empty table. The symptom — "badge says 1, table has no rows" — reads as an application
    /// bug and is really a synchronisation bug in the test.
    /// </para>
    /// <para>
    /// Waiting for the response ties the method's completion to the effect it claims to have had.
    /// A fixed delay would paper over the same race and would still fail on a slow CI runner.
    /// Adding a new product issues a POST; adding one already in the basket issues a PUT to bump
    /// the quantity, so both are accepted.
    /// </para>
    /// </remarks>
    private async Task AddCardToBasketAsync(ILocator card)
    {
        var addButton = card.Locator("button[aria-label='Add to Basket']");
        await Expect(addButton).ToBeVisibleAsync(VisibleOptions);

        var response = await ClickAndAwaitResponseAsync(addButton, "/api/BasketItems", "POST", "PUT");

        // A 401 here means the session was not live when the click fired. Left unchecked, the app
        // redirects to the login page and the *next* action fails looking for a product card that
        // is missing only because the browser is no longer on the catalogue — a failure that points
        // nowhere near the cause.
        if (!response.Ok)
        {
            throw new InvalidOperationException(
                $"Adding to the basket failed with HTTP {response.Status} ({response.StatusText}). " +
                "A 401 indicates the sign-in had not taken effect before this action.");
        }
    }

    private static async Task<ProductSummary> ReadCardAsync(ILocator card)
    {
        var name = (await card.Locator(".name").InnerTextAsync()).Trim();
        var priceText = (await card.Locator(".price").InnerTextAsync()).Trim();

        return new ProductSummary(name, ParsePrice(priceText));
    }

    /// <summary>
    /// Extracts a decimal from a rendered price such as <c>1.99¤</c>.
    /// </summary>
    /// <remarks>
    /// Juice Shop renders its own currency glyph rather than a culture-aware currency symbol, so
    /// <c>decimal.Parse</c> with a currency style will not handle it. Stripping to digits and the
    /// decimal separator is both simpler and stable across locales.
    /// </remarks>
    private static decimal ParsePrice(string priceText)
    {
        var digits = new string([.. priceText.Where(static c => char.IsAsciiDigit(c) || c is '.')]);

        return decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
            ? price
            : 0m;
    }
}
