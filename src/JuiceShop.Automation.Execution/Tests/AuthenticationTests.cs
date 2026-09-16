using JuiceShop.Automation.Definition.Flows;
using JuiceShop.Automation.Definition.TestData;
using NUnit.Framework;

namespace JuiceShop.Automation.Execution.Tests;

/// <summary>Sign-in, sign-out and registration journeys.</summary>
[TestFixture]
[Category(TestCategories.Smoke)]
[Category(TestCategories.Authentication)]
public sealed class AuthenticationTests : JuiceShopTest
{
    [Test]
    [Description("A customer with valid credentials can sign in and the navigation reflects it.")]
    public async Task Registered_Customer_Can_Sign_In()
    {
        await Shop.LoginAsAsync(TestUsers.Customer);

        await Shop.Navigation.ShouldShowSignedInAsync();
    }

    [Test]
    [Description("Sign-in is rejected for credentials that do not match an account.")]
    public async Task Sign_In_Is_Rejected_For_Unknown_Credentials()
    {
        await Shop.AttemptLoginAsync(TestUsers.Invalid);

        await Shop.LoginPage.ShouldShowInvalidCredentialsErrorAsync();
        await Shop.Navigation.ShouldShowSignedOutAsync();
    }

    [Test]
    [Category(TestCategories.Regression)]
    [Description("A signed-in customer can sign out again.")]
    public async Task Signed_In_Customer_Can_Sign_Out()
    {
        await Shop.LoginAsAsync(TestUsers.Customer);
        await Shop.Navigation.ShouldShowSignedInAsync();

        await Shop.LogoutAsync();

        await Shop.Navigation.ShouldShowSignedOutAsync();
    }

    [Test]
    [Category(TestCategories.Regression)]
    [Description("A brand new account can be registered and immediately used to sign in.")]
    public async Task New_Customer_Can_Register_And_Sign_In()
    {
        // A fresh account per run, rather than a shared seeded one. Allows the suite run repeatedly against the same container without a reset between runs.
        var account = NewAccount();

        await Shop.RegisterAndLoginAsync(account);

        await Shop.Navigation.ShouldShowSignedInAsync();
    }
}
