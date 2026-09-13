using System;
using System.Collections.Generic;
using System.Linq;
using SPKBultenAnaliz.Business.Security;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// SifreHashService birim testleri. PBKDF2 tabanlı şifre hashleme,
    /// doğrulama ve rastgele şifre üretimi işlemlerini test eder.
    /// </summary>
    public class SifreHashServiceTests
    {
        #region Hashle Tests

        [Fact]
        public void Hashle_ValidPassword_ReturnsHashWithThreeComponents()
        {
            // Arrange
            var sifre = "ValidPassword123";

            // Act
            var hash = SifreHashService.Hashle(sifre);

            // Assert
            Assert.NotEmpty(hash);
            var components = hash.Split('.');
            Assert.Equal(3, components.Length);
        }

        [Fact]
        public void Hashle_SamePasswordProducesDifferentHashes()
        {
            // Arrange
            var sifre = "TestPassword123";

            // Act
            var hash1 = SifreHashService.Hashle(sifre);
            var hash2 = SifreHashService.Hashle(sifre);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Hashle_EmptyPassword_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => SifreHashService.Hashle(""));
        }

        [Fact]
        public void Hashle_NullPassword_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => SifreHashService.Hashle(null!));
        }

        [Fact]
        public void Hashle_WhitespacePassword_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => SifreHashService.Hashle("   "));
        }

        [Fact]
        public void Hashle_LongPassword_ReturnsValidHash()
        {
            // Arrange
            var uzunSifre = new string('A', 1000);

            // Act
            var hash = SifreHashService.Hashle(uzunSifre);

            // Assert
            Assert.NotEmpty(hash);
            Assert.Contains(".", hash);
        }

        [Fact]
        public void Hashle_SpecialCharacters_ReturnsValidHash()
        {
            // Arrange
            var sifre = "P@$$wØrd!#%&*()[]{}";

            // Act
            var hash = SifreHashService.Hashle(sifre);

            // Assert
            Assert.NotEmpty(hash);
        }

        [Fact]
        public void Hashle_TurkishCharacters_ReturnsValidHash()
        {
            // Arrange
            var sifre = "ŞifreÜretiğiTestÇalışıyor";

            // Act
            var hash = SifreHashService.Hashle(sifre);

            // Assert
            Assert.NotEmpty(hash);
        }

        #endregion

        #region Dogrula Tests

        [Fact]
        public void Dogrula_CorrectPassword_ReturnsTrue()
        {
            // Arrange
            var sifre = "TestPassword123";
            var hash = SifreHashService.Hashle(sifre);

            // Act
            var result = SifreHashService.Dogrula(sifre, hash);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Dogrula_IncorrectPassword_ReturnsFalse()
        {
            // Arrange
            var sifre = "CorrectPassword123";
            var yanlisSifre = "WrongPassword123";
            var hash = SifreHashService.Hashle(sifre);

            // Act
            var result = SifreHashService.Dogrula(yanlisSifre, hash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_EmptyPassword_ReturnsFalse()
        {
            // Arrange
            var hash = SifreHashService.Hashle("ValidPassword123");

            // Act
            var result = SifreHashService.Dogrula("", hash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_NullPassword_ReturnsFalse()
        {
            // Arrange
            var hash = SifreHashService.Hashle("ValidPassword123");

            // Act
            var result = SifreHashService.Dogrula(null!, hash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_NullHash_ReturnsFalse()
        {
            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_EmptyHash_ReturnsFalse()
        {
            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", "");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_MalformedHash_ReturnsFalse()
        {
            // Arrange
            var malformedHash = "invalid.hash.format.with.too.many.parts";

            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", malformedHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_HashMissingComponents_ReturnsFalse()
        {
            // Arrange
            var incompleteHash = "210000.invalidbase64"; // Only 2 parts instead of 3

            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", incompleteHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_InvalidIterationCount_ReturnsFalse()
        {
            // Arrange
            var invalidHash = "notanumber.dGVzdA==.dGVzdA==";

            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", invalidHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_InvalidBase64Salt_ReturnsFalse()
        {
            // Arrange
            var invalidHash = "210000.!!!invalid!!!.dGVzdA==";

            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", invalidHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_InvalidBase64Hash_ReturnsFalse()
        {
            // Arrange
            var invalidHash = "210000.dGVzdA==.!!!invalid!!!";

            // Act
            var result = SifreHashService.Dogrula("AnyPassword123", invalidHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_TamperedHash_ReturnsFalse()
        {
            // Arrange
            var sifre = "TestPassword123";
            var hash = SifreHashService.Hashle(sifre);
            var components = hash.Split('.');
            // İlk bölümü (iterasyon sayısı) değiştir
            var tamperedHash = "100000." + components[1] + "." + components[2];

            // Act
            var result = SifreHashService.Dogrula(sifre, tamperedHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_PasswordSaltSwapped_ReturnsFalse()
        {
            // Arrange
            var sifre = "TestPassword123";
            var hash = SifreHashService.Hashle(sifre);
            var components = hash.Split('.');
            // Salt ve hash'i değiştir
            var tamperedHash = components[0] + "." + components[2] + "." + components[1];

            // Act
            var result = SifreHashService.Dogrula(sifre, tamperedHash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Dogrula_MultipleAttempts_ShouldNotTiming()
        {
            // Arrange
            var correctPassword = "CorrectPassword123";
            var incorrectPassword = "IncorrectPassword123";
            var hash = SifreHashService.Hashle(correctPassword);

            // Act - Test that both operations complete in reasonable time
            var startTime = DateTime.UtcNow;
            var correctResult = SifreHashService.Dogrula(correctPassword, hash);
            var correctDuration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            startTime = DateTime.UtcNow;
            var incorrectResult = SifreHashService.Dogrula(incorrectPassword, hash);
            var incorrectDuration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Assert
            Assert.True(correctResult);
            Assert.False(incorrectResult);
            // Execution times should be similar (within reasonable bounds)
            // to prevent timing attacks
            Assert.True(Math.Abs(correctDuration - incorrectDuration) < 1000, 
                $"Timing difference: {Math.Abs(correctDuration - incorrectDuration)}ms");
        }

        #endregion

        #region RastgeleSifreUret Tests

        [Fact]
        public void RastgeleSifreUret_DefaultLength_ReturnsLength14()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret();

            // Assert
            Assert.Equal(14, sifre.Length);
        }

        [Fact]
        public void RastgeleSifreUret_CustomLength_ReturnsCorrectLength()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret(20);

            // Assert
            Assert.Equal(20, sifre.Length);
        }

        [Fact]
        public void RastgeleSifreUret_MultipleCalls_ReturnsDifferentPasswords()
        {
            // Act
            var sifre1 = SifreHashService.RastgeleSifreUret();
            var sifre2 = SifreHashService.RastgeleSifreUret();

            // Assert
            Assert.NotEqual(sifre1, sifre2);
        }

        [Fact]
        public void RastgeleSifreUret_OnlyValidCharacters()
        {
            // Arrange
            const string ValidCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";

            // Act
            var sifre = SifreHashService.RastgeleSifreUret();

            // Assert
            Assert.True(sifre.Length > 0);
            foreach (var ch in sifre)
            {
                Assert.Contains(ch, ValidCharacters);
            }
        }

        [Fact]
        public void RastgeleSifreUret_MinimumLength()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret(1);

            // Assert
            Assert.Equal(1, sifre.Length);
        }

        [Fact]
        public void RastgeleSifreUret_LargeLength()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret(256);

            // Assert
            Assert.Equal(256, sifre.Length);
        }

        [Fact]
        public void RastgeleSifreUret_ContainsUppercase()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret(100);

            // Assert - With 100 characters, we should have at least some uppercase
            Assert.NotEmpty(sifre.Where(ch => char.IsUpper(ch)));
        }

        [Fact]
        public void RastgeleSifreUret_ContainsNumber()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret(100);

            // Assert - With 100 characters, we should have at least some numbers
            Assert.NotEmpty(sifre.Where(ch => char.IsDigit(ch)));
        }

        [Fact]
        public void RastgeleSifreUret_ContainsSpecialCharacter()
        {
            // Act
            var sifre = SifreHashService.RastgeleSifreUret(100);

            // Assert - With 100 characters, we should have at least some special chars
            Assert.NotEmpty(sifre.Where(ch => "!@#$%".Contains(ch)));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Hashle_And_Dogrula_RoundTrip_WithComplexPassword()
        {
            // Arrange
            var complexPassword = "C0mpl3x!P@$$w0rd#With$peci@l&Char$";

            // Act
            var hash = SifreHashService.Hashle(complexPassword);
            var isValid = SifreHashService.Dogrula(complexPassword, hash);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void MultipleHashes_AllUnique()
        {
            // Arrange
            var sifre = "TestPassword123";
            var hashSet = new HashSet<string>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                hashSet.Add(SifreHashService.Hashle(sifre));
            }

            // Assert
            Assert.Equal(10, hashSet.Count); // All hashes should be unique
        }

        #endregion
    }
}
