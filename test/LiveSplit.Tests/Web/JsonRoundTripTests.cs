using System.Collections.Generic;

using LiveSplit.Web;

using Xunit;

namespace LiveSplit.Tests.Web;

/// <summary>
/// Guards the System.Text.Json migration in <see cref="JSON"/> against silent regressions
/// versus the legacy JavaScriptSerializer behavior. Three things matter for callers that
/// hold dynamic references to deserialized values:
///   1. Integers come back as <see cref="long"/>, not int.
///   2. Fractional numbers come back as <see cref="double"/>, not decimal.
///   3. Lenient parsing (trailing commas, // comments) succeeds; strict parsing would have
///      surfaced as silent autosplitter config failures.
/// </summary>
public class JsonRoundTripTests
{
    [Fact]
    public void IntegerNumbers_DeserializeAsLong()
    {
        dynamic v = JSON.FromString("{\"x\":42}");
        object x = v.x;
        Assert.IsType<long>(x);
        Assert.Equal(42L, (long)x);
    }

    [Fact]
    public void FractionalNumbers_DeserializeAsDouble()
    {
        dynamic v = JSON.FromString("{\"pb\":123.456}");
        object pb = v.pb;
        Assert.IsType<double>(pb);
        Assert.Equal(123.456, (double)pb);
    }

    [Fact]
    public void TrailingCommas_AreTolerated()
    {
        dynamic v = JSON.FromString("{\"a\":1,\"b\":2,}");
        Assert.Equal(1L, (long)v.a);
        Assert.Equal(2L, (long)v.b);
    }

    [Fact]
    public void LineAndBlockComments_AreSkipped()
    {
        const string json = "{ /* leading */ \"a\":1, // trailing\n\"b\":2 }";
        dynamic v = JSON.FromString(json);
        Assert.Equal(1L, (long)v.a);
        Assert.Equal(2L, (long)v.b);
    }

    [Fact]
    public void ArraysProduceList_NotArrayList()
    {
        dynamic v = JSON.FromString("{\"items\":[1,2,3]}");
        var items = (IList<object>)v.items;
        Assert.Collection(items,
            x => Assert.Equal(1L, x),
            x => Assert.Equal(2L, x),
            x => Assert.Equal(3L, x));
    }

    [Fact]
    public void EmptyAndNullStrings_ReturnNullDynamic()
    {
        Assert.Null((object)JSON.FromString(""));
        Assert.Null((object)JSON.FromString(null));
    }
}
