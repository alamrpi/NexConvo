using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NexConvo.BuildingBlocks.Infrastructure.Security;
using System;
using System.Collections.Generic;
using Xunit;

namespace NexConvo.Integrations.Application.UnitTests;

public class AesEncryptionServiceTests
{
    private readonly IConfiguration _configurationMock;
    private readonly string _masterKey = "0123456789012345678901234567890123456789"; // > 32 chars

    public AesEncryptionServiceTests()
    {
        _configurationMock = Substitute.For<IConfiguration>();
        _configurationMock["SecuritySettings:AiMasterKey"].Returns(_masterKey);
    }

    [Fact]
    public void Encrypt_And_Decrypt_ShouldReturnOriginalString()
    {
        // Arrange
        var service = new AesEncryptionService(_configurationMock);
        var originalText = "sk-ant-api03-abcdefghijklmnop";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        encrypted.Should().NotBe(originalText);
        decrypted.Should().Be(originalText);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenKeyIsTooShort()
    {
        // Arrange
        var config = Substitute.For<IConfiguration>();
        config["SecuritySettings:AiMasterKey"].Returns("short-key");

        // Act
        Action act = () => { _ = new AesEncryptionService(config); };

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*must be at least 32 characters long*");
    }

    [Fact]
    public void Encrypt_ShouldReturnSameNullOrEmpty_WhenInputIsNullOrEmpty()
    {
        // Arrange
        var service = new AesEncryptionService(_configurationMock);

        // Act
        var resultNull = service.Encrypt(null!);
        var resultEmpty = service.Encrypt(string.Empty);

        // Assert
        resultNull.Should().BeNull();
        resultEmpty.Should().Be(string.Empty);
    }
}
