using FluentAssertions;
using Tinkwell.Firmwareless.PublicRepository.Authentication;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests
{
    public class ApiKeyHasherTests
    {
        [Fact]
        public void NewKeyBytes_ReturnsBase64StringOfCorrectLength()
        {
            // Arrange
            var expectedBytes = 32;

            // Act
            var key = ApiKeyHasher.NewKeyBytes(expectedBytes);

            // Assert
            key.Should().NotBeNullOrEmpty();
            Convert.FromBase64String(key).Length.Should().Be(expectedBytes);
        }

        [Fact]
        public void Hash_ReturnsHashAndSalt()
        {
            // Arrange
            var plainKey = "mysecretkey";

            // Act
            var (hash, salt) = ApiKeyHasher.Hash(plainKey);

            // Assert
            hash.Should().NotBeNullOrEmpty();
            salt.Should().NotBeNullOrEmpty();

            // Verify that hashing with the same key and returned salt produces the same hash
            var rehashed = ApiKeyHasher.HashWithSalt(plainKey, salt);
            rehashed.Should().Be(hash);
        }

        [Fact]
        public void HashWithSalt_ReturnsConsistentHash()
        {
            // Arrange
            var plainKey = "anothersecretkey";
            var salt = ApiKeyHasher.NewKeyBytes(16); // Use a fixed salt for consistency check

            // Act
            var hash1 = ApiKeyHasher.HashWithSalt(plainKey, salt);
            var hash2 = ApiKeyHasher.HashWithSalt(plainKey, salt);

            // Assert
            hash1.Should().NotBeNullOrEmpty();
            hash1.Should().Be(hash2);
        }

        [Fact]
        public void FixedTimeEquals_EqualStrings_ReturnsTrue()
        {
            // Arrange
            var str1 = "SGVsbG8gV29ybGQ=";
            var str2 = "SGVsbG8gV29ybGQ=";

            // Act
            var result = ApiKeyHasher.FixedTimeEquals(str1, str2);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void FixedTimeEquals_DifferentStrings_ReturnsFalse()
        {
            // Arrange
            var str1 = "SGVsbG8gV29ybGQ=";
            var str2 = "R29vZGJ5ZSBXb3JsZA==";

            // Act
            var result = ApiKeyHasher.FixedTimeEquals(str1, str2);

            // Assert
            result.Should().BeFalse();
        }
    }
}
