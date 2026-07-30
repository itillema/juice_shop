using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>Page object for the toolbar present on every route.</summary>
internal sealed class NavigationBar : PageObjectBase, INavigationBar
{
    public NavigationBar(IPage page, int expectTimeoutMilliseconds)
        : base(page, expectTimeoutMilliseconds)
    {
    }

    private ILocator AccountMenuButton => Page.Locator("#navbarAccount");

    private ILocator LoginMenuItem => Page.Locator("#navbarLoginButton");

    private ILocator LogoutMenuItem => Page.Locator("#navbarLogoutButton");

    private ILocator BasketButton => Page.Locator("button[routerlink='/basket']");

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await OpenAccountMenuAsync(LogoutMenuItem);
        await LogoutMenuItem.ClickAsync();
    }

    public async Task GoToLoginAsync(CancellationToken cancellationToken = default)
    {
        await OpenAccountMenuAsync(LoginMenuItem);
        await LoginMenuItem.ClickAsync();
    }

    public async Task GoToBasketAsync(CancellationToken cancellationToken = default)
    {
        await BasketButton.ClickAsync();
    }

    /// <summary>
    /// Signed-in state is inferred from the account menu offering "Logout" rather than "Login".
    /// </summary>
    /// <remarks>
    /// The menu is a Material overlay, so it has to be opened for the item to exist in the DOM at
    /// all. It is closed again afterwards because an open overlay covers the page and would
    /// intercept the next click — a failure that shows up in the following action, not this one.
    /// </remarks>
    public async Task ShouldShowSignedInAsync(CancellationToken cancellationToken = default)
    {
        await OpenAccountMenuAsync(LogoutMenuItem);
        await Expect(LogoutMenuItem).ToBeVisibleAsync(VisibleOptions);
        await CloseOverlayAsync();
    }

    public async Task ShouldShowSignedOutAsync(CancellationToken cancellationToken = default)
    {
        await OpenAccountMenuAsync(LoginMenuItem);
        await Expect(LoginMenuItem).ToBeVisibleAsync(VisibleOptions);
        await CloseOverlayAsync();
    }

    public Task ShouldShowBasketItemCountAsync(int expected, CancellationToken cancellationToken = default) =>
        Expect(BasketButton).ToContainTextAsync(
            expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new LocatorAssertionsToContainTextOptions { Timeout = ExpectTimeout });

    /// <summary>
    /// Opens the account menu, retrying if the click is swallowed before Angular Material has
    /// armed the trigger.
    /// </summary>
    /// <param name="expectedItem">A menu item that only exists once the overlay is open.</param>
    private Task OpenAccountMenuAsync(ILocator expectedItem) =>
        OpenOverlayAsync(AccountMenuButton, expectedItem);

    private async Task CloseOverlayAsync()
    {
        await Page.Keyboard.PressAsync("Escape");

        // Wait for the backdrop to actually leave the DOM. Returning while it is still animating
        // out hands the next action a page that is not yet clickable.
        await Expect(Page.Locator(".cdk-overlay-backdrop"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
    }
}
