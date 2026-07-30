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

        // Confirm the input survived. Angular can re-render this component out from under a fill —
        // most often just after registration routes here — which silently empties the fields and
        // turns the sign-in into a 401 that looks like bad credentials. This turns that into an
        // immediate, accurate failure. ToHaveValueAsync retries, so it also absorbs the ordinary
        // case where the model has simply not caught up with the DOM yet.
        await Expect(EmailField).ToHaveValueAsync(
            credentials.Email, new LocatorAssertionsToHaveValueOptions { Timeout = ExpectTimeout });

        // Waits for the authentication exchange to finish, whatever its outcome. A rejected
        // sign-in is a valid result here — the negative tests rely on it — so the status is not
        // checked; the assertions do that.
        await ClickAndAwaitResponseAsync(SubmitButton, "/rest/user/login", "POST");
    }

    /// <summary>
    /// Waits for the login form to be torn down, which happens only once the token has been stored
    /// and the application has routed away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Waiting for the login response is necessary but not sufficient. The response arriving means
    /// the server accepted the credentials; it does not mean the Angular app has processed the
    /// payload and attached the bearer token to its HTTP client. Acting in that window sends an
    /// unauthenticated request, which Juice Shop answers with 401 and a redirect back to login. The
    /// test then fails somewhere else entirely — typically on a product card that is "missing"
    /// only because the browser is no longer on the catalogue.
    /// </para>
    /// <para>
    /// The signal is the disappearance of the form, not a URL change. <c>WaitForURLAsync</c> is the
    /// obvious choice and does not work here: it waits for a navigation event, and Juice Shop routes
    /// by changing the hash, which is a same-document navigation that never fires one. It times out
    /// on every successful login.
    /// </para>
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
