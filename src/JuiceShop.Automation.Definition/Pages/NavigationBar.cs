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

    /// <summary>Signed in is inferred from the menu offering "Logout" rather than "Login".</summary>
    /// <remarks>Closed again afterwards, or the open overlay intercepts the next action's click.</remarks>
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

    /// <param name="expectedItem">A menu item that only exists once the overlay is open.</param>
    private Task OpenAccountMenuAsync(ILocator expectedItem) =>
        OpenOverlayAsync(AccountMenuButton, expectedItem);

    private async Task CloseOverlayAsync()
    {
        await Page.Keyboard.PressAsync("Escape");

        // A backdrop still animating out leaves the page unclickable.
        await Expect(Page.Locator(".cdk-overlay-backdrop"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
    }
}
