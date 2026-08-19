using JuiceShop.Automation.Definition.TestData;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>Page object for <c>/#/register</c>.</summary>
internal sealed class RegistrationPage : PageObjectBase, IRegistrationPage
{
    public RegistrationPage(IPage page, int expectTimeoutMilliseconds)
        : base(page, expectTimeoutMilliseconds)
    {
    }

    private ILocator EmailField => Page.Locator("#emailControl");

    private ILocator PasswordField => Page.Locator("#passwordControl");

    private ILocator RepeatPasswordField => Page.Locator("#repeatPasswordControl");

    /// <summary>No id on this control — only an aria-label.</summary>
    private ILocator SecurityQuestionSelect =>
        Page.Locator("mat-select[aria-label='Selection list for the security question']");

    private ILocator SecurityAnswerField => Page.Locator("#securityAnswerControl");

    private ILocator SubmitButton => Page.Locator("#registerButton");

    public Task OpenAsync(CancellationToken cancellationToken = default) =>
        NavigateAsync(JuiceShopRoutes.Register);

    public async Task RegisterAsync(RegistrationData registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await Expect(EmailField).ToBeVisibleAsync(VisibleOptions);

        await EmailField.FillAsync(registration.Email);
        await PasswordField.FillAsync(registration.Password);
        await RepeatPasswordField.FillAsync(registration.Password);

        await SelectFirstSecurityQuestionAsync();

        await SecurityAnswerField.FillAsync(registration.SecurityAnswer);

        var response = await ClickAndAwaitResponseAsync(SubmitButton, "/api/Users", "POST");

        // No legitimate failure here — a non-success status means the account does not exist.
        // Failing now names the cause instead of surfacing two steps later.
        if (!response.Ok)
        {
            var body = await response.TextAsync();
            throw new InvalidOperationException(
                $"Registering '{registration.Email}' failed with HTTP {response.Status} " +
                $"({response.StatusText}). Response body: {body}");
        }

        // The app routes itself to login on success. Returning before that hands the caller a page
        // about to be replaced, and Angular discards whatever it typed. See docs/architecture.md.
        await Expect(SubmitButton).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
    }

    public async Task ShouldBeDisplayedAsync(CancellationToken cancellationToken = default)
    {
        await Expect(EmailField).ToBeVisibleAsync(VisibleOptions);
        await Expect(SubmitButton).ToBeVisibleAsync(VisibleOptions);
    }

    /// <summary>Picks a security question from the Material select.</summary>
    /// <remarks>
    /// The arrow wrapper, not the select: the floating label sits over the control's hit area and
    /// intercepts pointer events. See docs/architecture.md — "Prefer the accessible path".
    /// Options render into a CDK overlay on the body, so they are located from the page root.
    /// </remarks>
    private async Task SelectFirstSecurityQuestionAsync()
    {
        var arrow = SecurityQuestionSelect.Locator(".mat-mdc-select-arrow-wrapper");
        var option = Page.Locator("mat-option").First;

        await OpenOverlayAsync(arrow, option);
        await option.ClickAsync();

        // The panel animates out over the answer field below, covering the next fill's target.
        await Expect(option).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
    }
}
