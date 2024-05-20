using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OneShelf.Telegram.Processor.Tests;

public class JsonTests
{
    [Fact]
    public void JRaw()
    {
        var json = "{a:3,b:[5, 6, {x:2,y:[3, 4, '5', {x:6}]}, null]}";

        var result = JsonConvert.DeserializeObject<Json>(json);

        Assert.Equal(4, result.B.Length);

        Assert.Equal("5", result.B[0].ToString());
        Assert.Equal("6", result.B[1].ToString());
        Assert.Equal("{\"x\":2,\"y\":[3,4,\"5\",{\"x\":6}]}", result.B[2].ToString());
        Assert.Equal("null", result.B[3].ToString());
    }

    private class Json
    {
        public int A { get; set; }

        public required JRaw[] B { get; set; }
    }
}