namespace JuiceShop.Automation.Definition.Pages;

/// <summary>
/// Client-side routes of the Juice Shop Angular application.
/// </summary>
/// <remarks>
/// The leading <c>/#/</c> is not decorative. Juice Shop uses hash-based routing, so navigating to
/// <c>/login</c> returns HTTP 200 and the SPA shell but never activates the login route — the test
/// then fails on a missing element with no indication that the navigation was the problem.
/// </remarks>
internal static class JuiceShopRoutes
{
    public const string Home = "/#/";
    public const string Login = "/#/login";
    public const string Register = "/#/register";
    public const string Search = "/#/search";
    public const string Basket = "/#/basket";
    public const string ScoreBoard = "/#/score-board";
}
