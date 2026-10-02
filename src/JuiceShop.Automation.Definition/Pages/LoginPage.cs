using JuiceShop.Automation.Definition.TestData;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>Page object for <c>/#/login</c>.</summary>
internal sealed class LoginPage : PageObjectBase, ILoginPage
{
    public LoginPage(IPage page, int expectTimeoutMilliseconds)
        : base(page, expectTimeoutMilliseconds)
    {
    }

    private ILocator EmailField => Page.Locator("#email");

    private ILocator PasswordField => Page.Locator("#password");

    private ILocator SubmitButton => Page.Locator("#loginButton");

    private ILocator RegistrationLink => Page.Locator("#newCustomerLink");

    private ILocator ErrorMessage => Page.Locator(".error");

    public Task OpenAsync(CancellationToken cancellationToken = default) =>
        NavigateAsync(JuiceShopRoutes.Login);

    public async Task SignInAsync(Credentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        await Expect(EmailField).ToBeVisibleAsync(VisibleOptions);

        await EmailField.FillAsync(credentials.Email);
        await PasswordField.FillAsync(credentials.Password);

        // Angular can re-render this form out from under the fill, emptying it, and turning sign-in into a 401 that looks like bad credentials.
        await Expect(EmailField).ToHaveValueAsync(
            credentials.Email, new LocatorAssertionsToHaveValueOptions { Timeout = ExpectTimeout });

        // Status unchecked: a rejected sign-in is valid here, and the assertions cover it.
        await ClickAndAwaitResponseAsync(SubmitButton, "/rest/user/login", "POST");
    }

    /// <summary>Waits for the form teardown that follows the token being stored.</summary>
    /// <remarks>
    /// The login response means the server accepted the credentials, not that Angular has attached the token — see docs/architecture.md. Signalled by the form disappearing, not by <c>WaitForURLAsync</c>, which never fires on Juice Shop's hash routing.
    /// </remarks>
    public Task WaitForSignInToCompleteAsync(CancellationToken cancellationToken = default) =>
        Expect(SubmitButton).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });

    public async Task GoToRegistrationAsync(CancellationToken cancellationToken = default)
    {
        await RegistrationLink.ClickAsync();
    }

    public Task ShouldShowInvalidCredentialsErrorAsync(CancellationToken cancellationToken = default) =>
        Expect(ErrorMessage).ToBeVisibleAsync(VisibleOptions);

    public async Task ShouldBeDisplayedAsync(CancellationToken cancellationToken = default)
    {
        await Expect(EmailField).ToBeVisibleAsync(VisibleOptions);
        await Expect(PasswordField).ToBeVisibleAsync(VisibleOptions);
        await Expect(SubmitButton).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = ExpectTimeout });
    }
}
