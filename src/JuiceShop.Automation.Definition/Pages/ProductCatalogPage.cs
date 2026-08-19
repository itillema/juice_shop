using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

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

        // The control stays expanded after a search, so expanding unconditionally works once and
        // hangs on every later search in the session.
        if (!await SearchInput.IsVisibleAsync())
        {
            await Expect(SearchToggle).ToBeVisibleAsync(VisibleOptions);
            await SearchToggle.ClickAsync();
        }

        await Expect(SearchInput).ToBeVisibleAsync(VisibleOptions);
        await SearchInput.FillAsync(searchTerm);
        await SearchInput.PressAsync("Enter");

        // Results are replaced in place, so there is no load event — the query landing in the URL
        // is the signal that the search was dispatched.
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
        // Both, because each alone is weak: an empty-state card still renders, and the message
        // element exists in the DOM before it is relevant.
        await Expect(ProductCards).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
        await Expect(NoResultsText).ToBeVisibleAsync(VisibleOptions);
    }

    public Task ShouldDisplayProductAsync(string productName, CancellationToken cancellationToken = default) =>
        Expect(ProductTile(productName).First).ToBeVisibleAsync(VisibleOptions);

    /// <summary>Clicks a card's add button and waits for the basket write to complete.</summary>
    /// <remarks>
    /// Navigating without waiting races the request — the badge updates optimistically while the
    /// basket renders empty. POST adds a new product, PUT bumps an existing one, so both are
    /// accepted. See docs/architecture.md.
    /// </remarks>
    private async Task AddCardToBasketAsync(ILocator card)
    {
        var addButton = card.Locator("button[aria-label='Add to Basket']");
        await Expect(addButton).ToBeVisibleAsync(VisibleOptions);

        var response = await ClickAndAwaitResponseAsync(addButton, "/api/BasketItems", "POST", "PUT");

        // Unchecked, a 401 redirects to login and the *next* action fails nowhere near the cause.
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

    /// <summary>Extracts a decimal from a rendered price such as <c>1.99¤</c>.</summary>
    /// <remarks>Juice Shop renders its own currency glyph, which a currency-style parse rejects.</remarks>
    private static decimal ParsePrice(string priceText)
    {
        var digits = new string([.. priceText.Where(static c => char.IsAsciiDigit(c) || c is '.')]);

        return decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
            ? price
            : 0m;
    }
}
