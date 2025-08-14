using FluentAssertions;
using Xunit;
using Tinkwell.Firmwareless.PublicRepository.Authentication;
using System;
using Tinkwell.Firmwareless.PublicRepository.Configuration;

namespace Tinkwell.Firmwareless.PublicRepository.UnitTests
{
    public class ApiKeyFormatTests
    {
        private readonly ApiKeyOptions _options = new()
        {
            KeyPrefix = "ak_",
            HmacSecret = "supersecretkeyforhmac",
            HmacBytes = 16
        };

        [Fact]
        public void Generate_ValidInput_ReturnsFormattedKey()
        {
            // Arrange
            var keyId = Guid.NewGuid();

            // Act
            var apiKey = ApiKeyFormat.Generate(keyId, _options);

            // Assert
            apiKey.Should().StartWith(_options.KeyPrefix);
            apiKey.Should().NotBeNullOrEmpty();
            apiKey.Length.Should().BeGreaterThan(_options.KeyPrefix.Length + 20); // Base64 encoded length
        }

        [Fact]
        public void TryParseAndValidate_ValidKey_ReturnsTrueAndCorrectKeyId()
        {
            // Arrange
            var keyId = Guid.NewGuid();
            var generatedKey = ApiKeyFormat.Generate(keyId, _options);

            // Act
            var isValid = ApiKeyFormat.TryParseAndValidate(generatedKey, _options, out var parsedKeyId);

            // Assert
            isValid.Should().BeTrue();
            parsedKeyId.Should().Be(keyId);
        }

        [Fact]
        public void TryParseAndValidate_InvalidKey_ReturnsFalse()
        {
            // Arrange
            var invalidKey = "ak_invalidkey";

            // Act
            var isValid = ApiKeyFormat.TryParseAndValidate(invalidKey, _options, out var parsedKeyId);

            // Assert
            isValid.Should().BeFalse();
            parsedKeyId.Should().Be(Guid.Empty);
        }

        [Fact]
        public void TryParseAndValidate_KeyWithWrongPrefix_ReturnsFalse()
        {
            // Arrange
            var keyId = Guid.NewGuid();
            var generatedKey = ApiKeyFormat.Generate(keyId, _options);
            var keyWithWrongPrefix = "wrong_" + generatedKey.Substring(_options.KeyPrefix.Length);

            // Act
            var isValid = ApiKeyFormat.TryParseAndValidate(keyWithWrongPrefix, _options, out var parsedKeyId);

            // Assert
            isValid.Should().BeFalse();
            parsedKeyId.Should().Be(Guid.Empty);
        }

        [Fact]
        public void TryParseAndValidate_KeyWithTamperedHmac_ReturnsFalse()
        {
            // Arrange
            var keyId = Guid.NewGuid();
            var generatedKey = ApiKeyFormat.Generate(keyId, _options);
            var parts = generatedKey.Split('_');
            var base64Payload = parts[1];
            var decodedPayload = Base64Url.Decode(base64Payload);

            // Tamper with HMAC part (last few bytes)
            decodedPayload[^1] = (byte)(decodedPayload[^1] + 1);

            var tamperedKey = _options.KeyPrefix + Base64Url.Encode(decodedPayload);

            // Act
            var isValid = ApiKeyFormat.TryParseAndValidate(tamperedKey, _options, out var parsedKeyId);

            // Assert
            isValid.Should().BeFalse();
            parsedKeyId.Should().Be(Guid.Empty);
        }
    }
}
