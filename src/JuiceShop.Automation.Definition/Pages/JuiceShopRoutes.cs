namespace JuiceShop.Automation.Definition.Pages;

/// <summary>Client-side routes of the Juice Shop Angular application.</summary>
/// <remarks>
/// The <c>/#/</c> is load-bearing: hash routing means <c>/login</c> returns 200 and the SPA shell
/// but never activates the route.
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
