using JuiceShop.Automation.Definition.Flows;
using NUnit.Framework;

namespace JuiceShop.Automation.Definition.Tests;

/// <summary>
/// Shopping basket management.
/// </summary>
/// <remarks>
/// Every test here registers its own account rather than reusing a seeded one. That is not
/// ceremony: the seeded customers ship with items already in their baskets, so a shared account
/// would make "the basket contains one line" depend on what the seed happened to contain and on
/// whichever test ran previously. A fresh account starts with an empty basket by construction, and
/// the tests stay order-independent and safe to run in parallel.
/// </remarks>
[TestFixture]
[Category(TestCategories.Basket)]
public sealed class BasketTests : JuiceShopTest
{
    private const string Product = "Apple Juice (1000ml)";
    private const string OtherProduct = "Banana Juice (1000ml)";

    [Test]
    [Category(TestCategories.Smoke)]
    [Description("A customer can add a searched-for product to their basket.")]
    public async Task Customer_Can_Add_Product_To_Basket()
    {
        await Shop.RegisterAndLoginAsync(NewAccount());

        await Shop.SearchAsync("Apple Juice");
        await Shop.AddToBasketAsync(Product);

        await Shop.OpenBasketAsync();
        await Shop.Basket.ShouldContainAsync(Product);
        await Shop.Basket.ShouldShowQuantityAsync(Product, 1);
    }

    [Test]
    [Description("A new customer's basket starts empty.")]
    public async Task New_Customer_Basket_Starts_Empty()
    {
        await Shop.RegisterAndLoginAsync(NewAccount());

        await Shop.OpenBasketAsync();

        await Shop.Basket.ShouldHaveLineCountAsync(0);
    }

    [Test]
    [Description("Adding a product twice increases its quantity rather than adding a second line.")]
    public async Task Adding_Same_Product_Twice_Increases_Quantity()
    {
        await Shop.RegisterAndLoginAsync(NewAccount());

        await Shop.SearchAsync("Apple Juice");
        await Shop.AddToBasketAsync(Product);
        await Shop.AddToBasketAsync(Product);

        await Shop.OpenBasketAsync();
        await Shop.Basket.ShouldHaveLineCountAsync(1);
        await Shop.Basket.ShouldShowQuantityAsync(Product, 2);
    }

    [Test]
    [Description("Quantity can be increased from within the basket.")]
    public async Task Quantity_Can_Be_Increased_From_The_Basket()
    {
        await Shop.RegisterAndLoginAsync(NewAccount());

        await Shop.SearchAsync("Apple Juice");
        await Shop.AddToBasketAsync(Product);
        await Shop.OpenBasketAsync();

        await Shop.Basket.IncreaseQuantityAsync(Product);

        await Shop.Basket.ShouldShowQuantityAsync(Product, 2);
    }

    [Test]
    [Description("A product can be removed from the basket.")]
    public async Task Product_Can_Be_Removed_From_The_Basket()
    {
        await Shop.RegisterAndLoginAsync(NewAccount());

        await Shop.SearchAsync("Apple Juice");
        await Shop.AddToBasketAsync(Product);
        await Shop.OpenBasketAsync();
        await Shop.Basket.ShouldContainAsync(Product);

        await Shop.Basket.RemoveAsync(Product);

        await Shop.Basket.ShouldNotContainAsync(Product);
        await Shop.Basket.ShouldHaveLineCountAsync(0);
    }

    [Test]
    [Description("Distinct products occupy distinct basket lines.")]
    public async Task Distinct_Products_Occupy_Distinct_Lines()
    {
        await Shop.RegisterAndLoginAsync(NewAccount());

        await Shop.SearchAsync("Juice");
        await Shop.AddToBasketAsync(Product);
        await Shop.AddToBasketAsync(OtherProduct);

        await Shop.OpenBasketAsync();
        await Shop.Basket.ShouldContainAsync(Product);
        await Shop.Basket.ShouldContainAsync(OtherProduct);
        await Shop.Basket.ShouldHaveLineCountAsync(2);
    }
}
