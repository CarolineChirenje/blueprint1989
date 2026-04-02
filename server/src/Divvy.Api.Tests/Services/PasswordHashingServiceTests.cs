using Divvy.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Divvy.Api.Tests.Services;

[TestClass]
public class PasswordHashingServiceTests
{
    private IPasswordHashingService _passwordHashingService;

    [TestInitialize]
    public void Setup()
    {
        _passwordHashingService = new PasswordHashingService();
    }

    [TestMethod]
    public void HashPassword_ShouldGenerateValidHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.IsNotNull(hash);
        Assert.IsTrue(hash.Length > 0);
        Assert.AreNotEqual(password, hash);
    }

    [TestMethod]
    public void HashPassword_SamePasswordsShouldGenerateDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _passwordHashingService.HashPassword(password);
        var hash2 = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.AreNotEqual(hash1, hash2, "Same passwords should generate different hashes due to salt");
    }

    [TestMethod]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHashingService.HashPassword(password);

        // Act
        var isValid = _passwordHashingService.VerifyPassword(password, hash);

        // Assert
        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _passwordHashingService.HashPassword(password);

        // Act
        var isValid = _passwordHashingService.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void VerifyPassword_WithEmptyPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHashingService.HashPassword(password);

        // Act
        var isValid = _passwordHashingService.VerifyPassword("", hash);

        // Assert
        Assert.IsFalse(isValid);
    }
}
