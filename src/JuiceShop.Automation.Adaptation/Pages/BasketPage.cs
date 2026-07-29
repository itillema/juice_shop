using System.Globalization;
using JuiceShop.Automation.Adaptation.Contracts.Pages;
using JuiceShop.Automation.Adaptation.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Adaptation.Pages;

/// <summary>Page object for <c>/#/basket</c>.</summary>
/// <remarks>
/// The quantity controls in this table carry no id, no aria-label and no text — they are icon-only
/// buttons whose sole distinguishing feature is the Font Awesome class on the nested SVG. Selecting
/// them via <c>:has()</c> on that icon class is the most stable option available; matching on
/// position within the cell would break the moment a control is added or reordered.
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

            // The quantity cell also contains the two icon buttons, but those render as SVG and
            // contribute no text, so the cell's inner text is the number alone.
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
