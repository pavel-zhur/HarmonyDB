using Newtonsoft.Json;
using OneShelf.Frontend.SpecificModel;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OneShelf.Frontend.Web.Tests;

public class SpecificAttributesSerializationTests
{
    [Fact]
    public void Test1()
    {
        Test(new()
        {
            BadgeText = "a",
            SourceColor = SourceColor.Primary
        });
        Test(new()
        {
            BadgeText = null,
            SourceColor = SourceColor.Primary
        });

        void Test(FrontendAttributesV1 a)
        {
            Dictionary<string, object?> dictionary = new();
            a.ToDictionary(dictionary);
            var serialized = JsonSerializer.Serialize(dictionary);
            dictionary = JsonSerializer.Deserialize<Dictionary<string, object?>>(serialized)!;
            var b = dictionary.ToFrontendAttributesV1();

            Assert.Equivalent(a, b);
        }
    }
}