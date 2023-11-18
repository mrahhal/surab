using Surab.Analysis;

namespace Surab.Compilation;

public partial class TargetTests
{
    [Fact]
    public void GetStructLayout_1()
    {
        var target = CreateTarget();

        // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|
        // a|      -      |b                    |
        var structType = CreateStruct(
            [
                new("a", BuiltInTypeSystem.Char),
                new("b", BuiltInTypeSystem.Number),
            ]);

        var structLayout = target.GetStructLayout(structType);

        structLayout.Layout.Size.Should().Be(16.AsBytes());
        structLayout.Layout.Alignment.Should().Be(8.AsBytes());
        structLayout.Sections.Should().HaveCount(3);

        structLayout.Sections[0].Kind.Should().Be(StructLayoutSectionKind.Data);
        structLayout.Sections[0].Offset.Should().Be(0.AsBytes());
        structLayout.Sections[0].Size.Should().Be(1.AsBytes());
        structLayout.Sections[0].FieldIndex.Should().Be(0);

        structLayout.Sections[1].Kind.Should().Be(StructLayoutSectionKind.Padding);
        structLayout.Sections[1].Offset.Should().Be(1.AsBytes());
        structLayout.Sections[1].Size.Should().Be(7.AsBytes());
        structLayout.Sections[1].FieldIndex.Should().Be(-1);

        structLayout.Sections[2].Kind.Should().Be(StructLayoutSectionKind.Data);
        structLayout.Sections[2].Offset.Should().Be(8.AsBytes());
        structLayout.Sections[2].Size.Should().Be(8.AsBytes());
        structLayout.Sections[2].FieldIndex.Should().Be(1);
    }

    [Fact]
    public void GetStructLayout_2()
    {
        var target = CreateTarget();

        // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20|21|22|23|
        // a|      -      |b                    |c |         -          |
        var structType = CreateStruct(
            [
                new("a", BuiltInTypeSystem.Char),
                new("b", BuiltInTypeSystem.Number),
                new("a", BuiltInTypeSystem.Char),
            ]);

        var structLayout = target.GetStructLayout(structType);

        structLayout.Layout.Size.Should().Be(24.AsBytes());
        structLayout.Layout.Alignment.Should().Be(8.AsBytes());
        structLayout.Sections.Should().HaveCount(5);

        structLayout.Sections[3].Kind.Should().Be(StructLayoutSectionKind.Data);
        structLayout.Sections[3].Offset.Should().Be(16.AsBytes());
        structLayout.Sections[3].Size.Should().Be(1.AsBytes());
        structLayout.Sections[3].FieldIndex.Should().Be(2);

        structLayout.Sections[4].Kind.Should().Be(StructLayoutSectionKind.Padding);
        structLayout.Sections[4].Offset.Should().Be(17.AsBytes());
        structLayout.Sections[4].Size.Should().Be(7.AsBytes());
        structLayout.Sections[4].FieldIndex.Should().Be(-1);
    }
}
