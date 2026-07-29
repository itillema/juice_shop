using JuiceShop.Automation.Adaptation.Contracts.Pages;
using JuiceShop.Automation.Adaptation.Playwright;
using JuiceShop.Automation.Utility.TestData;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Adaptation.Pages;

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

    /// <summary>
    /// The security-question dropdown, which carries no id — only an aria-label.
    /// </summary>
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

        // Registration must be complete before the caller tries to sign in with the new account.
        // Returning early makes the subsequent login fail against an account the server has not
        // created yet, and the resulting failure points at whatever the test did next rather than
        // at registration.
        var response = await ClickAndAwaitResponseAsync(SubmitButton, "/api/Users", "POST");

        // Unlike sign-in, there is no legitimate way for registration to fail here — a non-success
        // status means the account does not exist. Letting that pass silently is how a registration
        // problem resurfaces two steps later as "the product card is not visible", on the login page,
        // with nothing pointing at the real cause. Failing here names it.
        if (!response.Ok)
        {
            var body = await response.TextAsync();
            throw new InvalidOperationException(
                $"Registering '{registration.Email}' failed with HTTP {response.Status} " +
                $"({response.StatusText}). Response body: {body}");
        }

        // On success the application routes itself to the login page. Returning before that has
        // happened hands the caller a page that is about to be replaced: it fills the login form,
        // Angular then re-renders the component and discards the input, and the submitted credentials
        // are empty. The sign-in gets a legitimate 401 and the test fails several steps later having
        // apparently "forgotten" the account it just created.
        await Expect(SubmitButton).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
    }

    public async Task ShouldBeDisplayedAsync(CancellationToken cancellationToken = default)
    {
        await Expect(EmailField).ToBeVisibleAsync(VisibleOptions);
        await Expect(SubmitButton).ToBeVisibleAsync(VisibleOptions);
    }

    /// <summary>
    /// Picks a security question from the Material select.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opening this dropdown is fiddlier than it looks, and the obvious approaches are all subtly
    /// wrong. Measured against this build:
    /// </para>
    /// <list type="bullet">
    /// <item>Clicking the select or its <c>.mat-mdc-select-trigger</c> fails: the floating
    /// <c>mat-label</c> sits inside the notched outline directly over the hit area, and Playwright
    /// correctly reports that it intercepts pointer events.</item>
    /// <item><c>PressAsync("Enter")</c> on the select fails: it focuses and sends the key within a
    /// single call, before Angular has processed the focus event and armed its key handling.</item>
    /// <item><c>FocusAsync()</c> followed by <c>Keyboard.PressAsync("Enter")</c> works only if a
    /// delay is inserted between them. That is a race, not a fix — it passes on an idle machine and
    /// fails under parallel load, which is exactly the flakiness profile that erodes trust in a
    /// suite.</item>
    /// </list>
    /// <para>
    /// Clicking the arrow wrapper is deterministic with no delay: it sits at the far right of the
    /// control, clear of the label, so the click lands on the first attempt and still goes through
    /// Playwright's full actionability check. No forced click, no sleep.
    /// </para>
    /// <para>
    /// Options render into a CDK overlay appended to the document body rather than inside the
    /// select, so they are located from the page root.
    /// </para>
    /// </remarks>
    private async Task SelectFirstSecurityQuestionAsync()
    {
        var arrow = SecurityQuestionSelect.Locator(".mat-mdc-select-arrow-wrapper");
        var option = Page.Locator("mat-option").First;

        await OpenOverlayAsync(arrow, option);
        await option.ClickAsync();

        // The panel animates out after selection, and it overlaps the answer field directly below.
        // Returning while it is still on screen hands the next fill a covered target.
        await Expect(option).ToHaveCountAsync(
            0, new LocatorAssertionsToHaveCountOptions { Timeout = ExpectTimeout });
    }
}
