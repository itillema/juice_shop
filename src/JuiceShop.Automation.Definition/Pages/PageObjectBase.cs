using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace JuiceShop.Automation.Definition.Pages;

/// <summary>
/// Shared plumbing for page objects: the page handle, a pre-configured assertion timeout, and
/// navigation.
/// </summary>
/// <remarks>
/// Page objects are deliberately <see langword="internal"/>. Only the technology-free interfaces in
/// <c>JuiceShop.Automation.Definition.Pages</c> escape the assembly, which is what stops a
/// test from reaching past the abstraction to poke at a locator. That constraint is enforced by the
/// compiler here and re-asserted by the architecture tests.
/// </remarks>
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

    /// <summary>Navigates to a hash route and waits for Angular to finish the initial render.</summary>
    protected async Task NavigateAsync(string route)
    {
        await Page.GotoAsync(route);
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    /// <summary>
    /// Opens an Angular Material overlay (menu or select panel), retrying if the first click has
    /// no effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A click is a single shot: it either lands on a live handler or it is lost. Angular Material
    /// components go through a window where the element is present, visible, stable and enabled —
    /// so Playwright's actionability checks all pass — but the component's own click handling is
    /// not yet armed. The click is then silently swallowed. The element even takes focus, which is
    /// what makes the failure so confusing to diagnose: a screenshot shows a focused control with
    /// a closed panel, and every locator involved is provably correct.
    /// </para>
    /// <para>
    /// Playwright's JavaScript API covers this with <c>expect.toPass()</c>, which retries an entire
    /// block. The .NET API has no equivalent, so this is the bounded equivalent. Two properties
    /// make it safe rather than a blind retry: it checks whether the overlay is already open before
    /// each attempt, so it can never toggle a successfully opened panel shut, and it gives up after
    /// a fixed number of attempts and then defers to <c>Expect</c> so the final failure carries
    /// Playwright's full diagnostic call log rather than a bare timeout.
    /// </para>
    /// <para>
    /// This retries an <em>interaction</em>, not an assertion. It cannot mask a genuine product
    /// bug: if the overlay never opens, the test still fails.
    /// </para>
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
                // Swallowed click. Fall through and try again.
            }
        }

        // Exhausted the retries: let Expect fail with its full call log.
        await Expect(overlayContent).ToBeVisibleAsync(VisibleOptions);
    }

    /// <summary>
    /// Clicks a control and waits for the request it triggers to come back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every click that changes server state is asynchronous, and a page object that returns as soon
    /// as the click lands is lying about what it did. The caller then navigates or asserts against
    /// state the server has not written yet. On a fast, idle machine the request usually wins the
    /// race and the suite looks green; under parallel execution or on a loaded CI runner it does
    /// not, which is precisely the profile of a test suite people stop trusting.
    /// </para>
    /// <para>
    /// Any status is accepted, not just success. A rejected login is a legitimate outcome that the
    /// negative tests depend on — what matters here is that the exchange finished, not that it
    /// succeeded. Asserting on the outcome is the caller's job.
    /// </para>
    /// </remarks>
    /// <param name="control">The control to click.</param>
    /// <param name="urlFragment">Substring identifying the expected request URL.</param>
    /// <param name="methods">Accepted HTTP methods.</param>
    /// <returns>The matched response, so the caller can assert on its status when that matters.</returns>
    protected async Task<IResponse> ClickAndAwaitResponseAsync(ILocator control, string urlFragment, params string[] methods)
    {
        return await Page.RunAndWaitForResponseAsync(
            async () => await control.ClickAsync(),
            response =>
                response.Url.Contains(urlFragment, StringComparison.OrdinalIgnoreCase)
                && methods.Contains(response.Request.Method, StringComparer.Ordinal),
            new PageRunAndWaitForResponseOptions { Timeout = ExpectTimeout });
    }

    /// <summary>
    /// Locates a product card by the name it displays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matching on the rendered name rather than an index is what makes these tests survive a
    /// change to the catalogue ordering or to the seeded product list.
    /// </para>
    /// <para>
    /// The selector is the Angular component element <c>app-product</c> rather than a CSS class.
    /// The class <c>.product</c> appears twice per card (on the host element and on the inner
    /// <c>article</c>), so it would double every count; <c>mat-card</c> also matches the empty-state
    /// card rendered when a search returns nothing.
    /// </para>
    /// </remarks>
    protected ILocator ProductTile(string productName) =>
        Page.Locator("app-product").Filter(new LocatorFilterOptions { HasTextString = productName });
}
