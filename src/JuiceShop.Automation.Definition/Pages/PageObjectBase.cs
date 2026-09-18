using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>Shared plumbing for page objects: page handle, assertion timeout, navigation.</summary>
/// <remarks>Internal by design — only the technology-free contracts leave this assembly.</remarks>
internal abstract class PageObjectBase
{
    protected PageObjectBase(IPage page, int expectTimeoutMilliseconds)
    {
        Page = page;
        ExpectTimeout = expectTimeoutMilliseconds;
    }

    protected IPage Page { get; }

    /// <summary>Timeout applied to auto-retrying assertions, in milliseconds.</summary>
    protected int ExpectTimeout { get; }

    /// <summary>Options carrying the configured assertion timeout.</summary>
    protected LocatorAssertionsToBeVisibleOptions VisibleOptions => new() { Timeout = ExpectTimeout };

    /// <summary>Navigates to a hash route and waits for Angular's initial render.</summary>
    protected async Task NavigateAsync(string route)
    {
        await Page.GotoAsync(route);
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    /// <summary>Opens a Material overlay, retrying a swallowed first click.</summary>
    /// <remarks>
    /// Material components pass every actionability check before their own click handling is armed, so the first click can be lost silently. Retries an interaction, not an assertion — if the overlay never opens the test still fails. See docs/architecture.md.
    /// </remarks>
    /// <param name="trigger">The control that opens the overlay.</param>
    /// <param name="overlayContent">An element that exists only once the overlay is open.</param>
    protected async Task OpenOverlayAsync(ILocator trigger, ILocator overlayContent)
    {
        const int maxAttempts = 4;
        const int perAttemptTimeoutMs = 2000;

        await Expect(trigger).ToBeVisibleAsync(VisibleOptions);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Checked first, so a successful open is never toggled shut.
            if (await overlayContent.IsVisibleAsync())
            {
                return;
            }

            await trigger.ClickAsync();

            try
            {
                await overlayContent.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = perAttemptTimeoutMs,
                });

                return;
            }
            catch (TimeoutException) when (attempt < maxAttempts)
            {
                // Swallowed click. Try again.
            }
        }

        // Exhausted: let Expect fail with its full call log rather than a bare timeout.
        await Expect(overlayContent).ToBeVisibleAsync(VisibleOptions);
    }

    /// <summary>Clicks a control and waits for the request it triggers to come back.</summary>
    /// <remarks>
    /// A page object that returns as soon as the click lands is lying about what it did. Any status is accepted — a rejected login is a legitimate outcome; asserting on it is the caller's job. See docs/architecture.md.
    /// </remarks>
    /// <returns>The matched response, so the caller can assert on its status.</returns>
    protected async Task<IResponse> ClickAndAwaitResponseAsync(ILocator control, string urlFragment, params string[] methods)
    {
        return await Page.RunAndWaitForResponseAsync(
            async () => await control.ClickAsync(),
            response =>
                response.Url.Contains(urlFragment, StringComparison.OrdinalIgnoreCase)
                && methods.Contains(response.Request.Method, StringComparer.Ordinal),
            new PageRunAndWaitForResponseOptions { Timeout = ExpectTimeout });
    }

    /// <summary>Locates a product card by the name it displays.</summary>
    /// <remarks>
    /// By name, not index, so the tests survive a catalogue reorder. <c>app-product</c> rather than <c>.product</c> (which appears twice per card) or <c>mat-card</c> (which matches the empty-state card).
    /// </remarks>
    protected ILocator ProductTile(string productName) =>
        Page.Locator("app-product").Filter(new LocatorFilterOptions { HasTextString = productName });
}
