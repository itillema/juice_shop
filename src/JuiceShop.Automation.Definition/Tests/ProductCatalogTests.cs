using JuiceShop.Automation.Definition.Flows;
using NUnit.Framework;

namespace JuiceShop.Automation.Definition.Tests;

/// <summary>Catalogue browsing and search.</summary>
[TestFixture]
[Category(TestCategories.Catalog)]
public sealed class ProductCatalogTests : JuiceShopTest
{
    [Test]
    [Category(TestCategories.Smoke)]
    [Description("Searching for a stocked product returns it in the results.")]
    public async Task Search_Returns_Matching_Product()
    {
        await Shop.SearchAsync("Apple Juice");

        await Shop.Catalog.ShouldShowResultsAsync();
        await Shop.Catalog.ShouldDisplayProductAsync("Apple Juice (1000ml)");
    }

    [Test]
    [Description("A search term matching nothing shows the empty state rather than stale results.")]
    public async Task Search_With_No_Matches_Shows_Empty_State()
    {
        await Shop.SearchAsync("verwijderdproductdatnietbestaat");

        await Shop.Catalog.ShouldShowNoResultsAsync();
    }

    [Test]
    [Description("A more specific search term yields no more results than a broader one.")]
    public async Task More_Specific_Search_Narrows_The_Result_Set()
    {
        await Shop.SearchAsync("Juice");
        var broadResults = await Shop.Catalog.GetVisibleProductsAsync();

        await Shop.SearchAsync("Eggfruit");
        var narrowResults = await Shop.Catalog.GetVisibleProductsAsync();

        // Asserting on materialised data, so NUnit's constraint model is the right tool here;
        // Playwright's Expect is for values still being read out of a live DOM.
        //
        // Deliberately an invariant about relative sizes rather than "every result name contains
        // the term". Juice Shop matches the term against name OR description, so a product whose
        // name does not contain the term is a correct result, not a bug — an earlier version of
        // this test asserted otherwise and was simply wrong about the application.
        Assert.That(broadResults, Is.Not.Empty);
        Assert.That(narrowResults, Is.Not.Empty);
        Assert.That(narrowResults, Has.Count.LessThanOrEqualTo(broadResults.Count));
    }

    [Test]
    [Description("Catalogue entries carry a positive price.")]
    public async Task Catalog_Products_Have_A_Price()
    {
        await Shop.SearchAsync("Juice");

        var products = await Shop.Catalog.GetVisibleProductsAsync();

        Assert.That(products.Select(product => product.Price), Has.All.GreaterThan(0m));
    }

    [TestCase("Apple Juice", "Apple Juice (1000ml)")]
    [TestCase("Banana Juice", "Banana Juice (1000ml)")]
    [TestCase("Eggfruit", "Eggfruit Juice (500ml)")]
    [Description("Several seeded products are individually findable by name.")]
    public async Task Seeded_Products_Are_Findable(string searchTerm, string expectedProduct)
    {
        await Shop.SearchAsync(searchTerm);

        await Shop.Catalog.ShouldDisplayProductAsync(expectedProduct);
    }
}
