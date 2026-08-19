using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>Page object for <c>/#/basket</c>.</summary>
/// <remarks>
/// The quantity controls have no id, aria-label or text, so they are selected by the icon class on
/// the nested SVG. Matching on position would break on any reorder.
/// </remarks>
internal sealed class BasketPage : PageObjectBase, IBasketPage
{
    private const string IncreaseIcon = ".fa-plus-square";
    private const string DecreaseIcon = ".fa-minus-square";
    private const string RemoveIcon = ".fa-trash-alt";

    public BasketPage(IPage page, int expectTimeoutMilliseconds)
        : base(page, expectTimeoutMilliseconds)
    {
    }

    /// <summary>Data rows only — the header renders as <c>mat-header-row</c>.</summary>
    private ILocator Rows => Page.Locator("mat-row");

    public Task OpenAsync(CancellationToken cancellationToken = default) =>
        NavigateAsync(JuiceShopRoutes.Basket);

    public async Task<IReadOnlyList<BasketLine>> GetLinesAsync(CancellationToken cancellationToken = default)
    {
        // An empty basket is a legitimate state, so this waits for the table rather than for a row.
        await Page.WaitForSelectorAsync("mat-table", new PageWaitForSelectorOptions
        {
            Timeout = ExpectTimeout,
            State = WaitForSelectorState.Attached,
        });

        var rows = await Rows.AllAsync();
        var lines = new List<BasketLine>(rows.Count);

        foreach (var row in rows)
        {
            var name = (await row.Locator(".mat-column-product").InnerTextAsync()).Trim();

            // The cell's icon buttons render as SVG and contribute no text.
            var quantityText = (await row.Locator(".mat-column-quantity").InnerTextAsync()).Trim();

            lines.Add(new BasketLine(
                name,
                int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
                    ? quantity
                    : 0));
        }

        return lines;
    }

    public Task IncreaseQuantityAsync(string productName, CancellationToken cancellationToken = default) =>
        ClickRowControlAsync(productName, IncreaseIcon);

    public Task DecreaseQuantityAsync(string productName, CancellationToken cancellationToken = default) =>
        ClickRowControlAsync(productName, DecreaseIcon);

    public Task RemoveAsync(string productName, CancellationToken cancellationToken = default) =>
        ClickRowControlAsync(productName, RemoveIcon);

    public Task ShouldContainAsync(string productName, CancellationToken cancellationToken = default) =>
        Expect(RowFor(productName)).ToBeVisibleAsync(VisibleOptions);

    public Task ShouldNotContainAsync(string productName, CancellationToken cancellationToken = default) =>
        Expect(RowFor(productName)).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });

    public Task ShouldShowQuantityAsync(string productName, int expected, CancellationToken cancellationToken = default) =>
        Expect(RowFor(productName).Locator(".mat-column-quantity")).ToHaveTextAsync(
            expected.ToString(CultureInfo.InvariantCulture),
            new LocatorAssertionsToHaveTextOptions { Timeout = ExpectTimeout, UseInnerText = true });

    public Task ShouldHaveLineCountAsync(int expected, CancellationToken cancellationToken = default) =>
        Expect(Rows).ToHaveCountAsync(
            expected, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });

    private async Task ClickRowControlAsync(string productName, string iconClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        var row = RowFor(productName);
        await Expect(row).ToBeVisibleAsync(VisibleOptions);

        var button = row.Locator($"button:has({iconClass})");
        await Expect(button).ToBeVisibleAsync(VisibleOptions);
        await button.ClickAsync();
    }

    private ILocator RowFor(string productName) =>
        Rows.Filter(new LocatorFilterOptions { HasTextString = productName });
}
