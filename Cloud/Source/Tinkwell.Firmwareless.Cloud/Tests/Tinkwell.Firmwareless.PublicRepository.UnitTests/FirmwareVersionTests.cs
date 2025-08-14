using FluentAssertions;
using Tinkwell.Firmwareless.PublicRepository.Repositories;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests
{
    public class FirmwareVersionTests
    {
        [Theory]
        [InlineData("1.0.0.0", 1, 0, 0, 0, null)]
        [InlineData("1.2.3.4", 1, 2, 3, 4, null)]
        [InlineData("65535.65535.4294967295.4294967295", 65535, 65535, 4294967295u, 4294967295u, null)]
        [InlineData("1.2.3.4-alpha", 1, 2, 3, 4, "alpha")]
        [InlineData("1.2-beta", 1, 2, 0, 0, "beta")]
        [InlineData("1", 1, 0, 0, 0, null)]
        public void Parse_ValidInput_ReturnsCorrectVersion(string input, int major, int minor, uint revision, uint build, string? suffix)
        {
            var version = FirmwareVersion.Parse(input);

            version.Major.Should().Be((ushort)major);
            version.Minor.Should().Be((ushort)minor);
            version.Revision.Should().Be((uint)revision);
            version.Build.Should().Be((uint)build);
            version.Suffix.Should().Be(suffix);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("1.2.3.4.5")]
        [InlineData("a.b.c.d")]
        [InlineData("1.2.3.a")]
        [InlineData("1/2")]
        [InlineData("1\\2")]
        [InlineData("65536.0.0.0")] // Major overflow
        [InlineData("0.65536.0.0")] // Minor overflow
        [InlineData("0.0.4294967296.0")] // Revision overflow
        [InlineData("0.0.0.4294967296")] // Build overflow
        public void Parse_InvalidInput_ThrowsFormatException(string? input)
        {
            Action act = () => FirmwareVersion.Parse(input!); // yes, it's on purpose to use '!' to pass a null value
            act.Should().Throw<FormatException>().WithMessage($"Invalid firmware version format: '{input}'");
        }

        [Theory]
        [InlineData("1.0.0.0", true)]
        [InlineData("1.2.3.4", true)]
        [InlineData("1.2.3.4-alpha", true)]
        [InlineData("1", true)]
        [InlineData("65535.65535.4294967295.4294967295", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData("1.2.3.4.5", false)]
        [InlineData("a.b.c.d", false)]
        [InlineData("1.2.3.a", false)]
        [InlineData("1/2", false)]
        [InlineData("1\\2", false)]
        [InlineData("65536.0.0.0", false)]
        [InlineData("0.65536.0.0", false)]
        [InlineData("0.0.4294967296.0", false)]
        [InlineData("0.0.0.4294967296", false)]
        public void Validate_Input_ReturnsExpectedResult(string? input, bool expected)
        {
            // yes, it's on purpose to use '!' to pass a null value
            FirmwareVersion.Validate(input!).Should().Be(expected);
        }

        [Theory]
        [InlineData("1.0", "1.0.0.0", 0)]
        [InlineData("1.0", "2.0", -1)]
        [InlineData("2.0", "1.0", 1)]
        [InlineData("1.1", "1.0.1", 1)]
        [InlineData("1.0.1", "1.1", -1)]
        [InlineData("1.0.0.1", "1.0.1", -1)]
        [InlineData("1.0.0.0-alpha", "1.0.0.0-beta", 0)] // Suffix is ignored
        public void CompareTo_WithOtherVersion_ReturnsExpected(string v1, string v2, int expected)
        {
            var version1 = FirmwareVersion.Parse(v1);
            var version2 = FirmwareVersion.Parse(v2);
            version1.CompareTo(version2).Should().Be(expected);
        }

        [Fact]
        public void CompareTo_WithNull_Returns1()
        {
            var version = FirmwareVersion.Parse("1.0");
            version.CompareTo(null).Should().Be(1);
        }

        [Theory]
        [InlineData("1.0", "1.0.0.0", true)]
        [InlineData("1.0", "2.0", false)]
        [InlineData("1.0.0.0-alpha", "1.0.0.0-beta", true)] // Suffix is ignored
        public void Equals_WithOtherVersion_ReturnsExpected(string v1, string v2, bool expected)
        {
            var version1 = FirmwareVersion.Parse(v1);
            var version2 = FirmwareVersion.Parse(v2);
            version1.Equals(version2).Should().Be(expected);
        }

        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            var version = FirmwareVersion.Parse("1.0");
            version.Equals(null).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithObject_ReturnsCorrectly()
        {
            var version = FirmwareVersion.Parse("1.0");
            version.Equals((object)FirmwareVersion.Parse("1.0")).Should().BeTrue();
            version.Equals("1.0").Should().BeFalse();
        }

        [Fact]
        public void GetHashCode_ForEqualObjects_IsEqual()
        {
            var version1 = FirmwareVersion.Parse("1.2.3.4-alpha");
            var version2 = FirmwareVersion.Parse("1.2.3.4-beta");
            version1.GetHashCode().Should().Be(version2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_ForDifferentObjects_IsDifferent()
        {
            var version1 = FirmwareVersion.Parse("1.2.3.4");
            var version2 = FirmwareVersion.Parse("4.3.2.1");
            version1.GetHashCode().Should().NotBe(version2.GetHashCode());
        }

        [Theory]
        [InlineData("1.0.0.0", "1")]
        [InlineData("1.2.0.0", "1.2")]
        [InlineData("1.2.3.0", "1.2.3")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        [InlineData("1.0.0.0-alpha", "1-alpha")]
        [InlineData("1.2.3.4-beta.1", "1.2.3.4-beta.1")]
        [InlineData("0.0.0.0", "0.0")]
        public void ToString_FormatsCorrectly(string input, string expected)
        {
            var version = FirmwareVersion.Parse(input);
            version.ToString().Should().Be(expected);
        }

        [Fact]
        public void Operators_WorkAsExpected()
        {
            var v1_0 = FirmwareVersion.Parse("1.0");
            var v1_0_clone = FirmwareVersion.Parse("1.0.0.0");
            var v2_0 = FirmwareVersion.Parse("2.0");
            FirmwareVersion? nullVersion = null;

            (v1_0 == v1_0_clone).Should().BeTrue();
            (v1_0 == v2_0).Should().BeFalse();
            (v1_0 != v2_0).Should().BeTrue();
            (v1_0 != v1_0_clone).Should().BeFalse();

            (v1_0 < v2_0).Should().BeTrue();
            (v2_0 < v1_0).Should().BeFalse();
            (v1_0 <= v2_0).Should().BeTrue();
            (v1_0 <= v1_0_clone).Should().BeTrue();

            (v2_0 > v1_0).Should().BeTrue();
            (v1_0 > v2_0).Should().BeFalse();
            (v2_0 >= v1_0).Should().BeTrue();
            (v1_0 >= v1_0_clone).Should().BeTrue();
            
            (nullVersion! == null!).Should().BeTrue();
            (v1_0 == nullVersion!).Should().BeFalse();
            (v1_0 != nullVersion!).Should().BeTrue();
        }

        public class VersionStringComparerTests
        {
            private readonly FirmwareVersion.VersionStringComparer _comparer = new();

            [Fact]
            public void Compare_BothNull_ReturnsZero()
            {
                _comparer.Compare(null, null).Should().Be(0);
            }

            [Fact]
            public void Compare_XNull_ReturnsMinusOne()
            {
                _comparer.Compare(null, "1.0").Should().Be(-1);
            }

            [Fact]
            public void Compare_YNull_ReturnsOne()
            {
                _comparer.Compare("1.0", null).Should().Be(1);
            }

            [Theory]
            [InlineData("1.0", "1.0", 0)]
            [InlineData("1.0", "2.0", -1)]
            [InlineData("2.0", "1.0", 1)]
            [InlineData("1.0.0-alpha", "1.0.0-beta", 0)]
            public void Compare_ValidStrings_ReturnsExpected(string x, string y, int expectedSign)
            {
                var result = _comparer.Compare(x, y);
                Math.Sign(result).Should().Be(expectedSign);
            }
        }
    }
}