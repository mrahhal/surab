namespace Surab.Analyzer;

public class PeekerTests
{
    [Fact]
    public void Empty()
    {
        var peeker = new Peeker<int>(Array.Empty<int>());
        var advanced = peeker.TryMoveNext(out _);
        advanced.Should().BeFalse();
    }

    [Fact]
    public void Basic()
    {
        var source = new int[] { 1, 2, 3 };
        var peeker = new Peeker<int>(source);
        peeker.ToList().Should().BeEquivalentTo(source);
    }

    [Fact]
    public void Int()
    {
        TestWith(new int[] { 1, 2, 3 });
    }

    [Fact]
    public void String()
    {
        TestWith(new string[] { "1", "2", "3" });
    }

    private static void TestWith<T>(T[] source)
        where T : notnull
    {
        if (source.Length < 3) throw new Exception();

        var peeker = new Peeker<T>(source);
        peeker.TryPeek(0, out var peeked).Should().BeTrue();
        peeked.Should().Be(source[0]);

        var advanced = peeker.TryMoveNext(out var result);
        advanced.Should().BeTrue();
        result.Should().Be(source[0]);
        peeker.Current.Should().Be(result);

        peeker.TryPeek(0, out peeked).Should().BeTrue();
        peeked.Should().Be(source[1]);
        peeker.TryPeek(1, out peeked).Should().BeTrue();
        peeked.Should().Be(source[2]);
        peeker.TryPeek(2, out _).Should().BeFalse();

        advanced = peeker.TryMoveNext(out result);
        advanced.Should().BeTrue();
        result.Should().Be(source[1]);
    }
}
