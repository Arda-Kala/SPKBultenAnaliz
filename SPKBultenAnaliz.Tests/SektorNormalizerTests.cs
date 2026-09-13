using System.Linq;
using SPKBultenAnaliz.Business.Services;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// SektorNormalizer birim testleri. Türkçe karakterli sektör adlarını
    /// kanonik forma normalize etme işlemlerini test eder.
    /// </summary>
    public class SektorNormalizerTests
    {
        #region Normalize - Exact Matches

        [Theory]
        [InlineData("Bankacılık", "Bankacılık")]
        [InlineData("Sigorta", "Sigorta")]
        [InlineData("Enerji", "Enerji")]
        [InlineData("Gayrimenkul (GYO)", "Gayrimenkul (GYO)")]
        [InlineData("Perakende", "Perakende")]
        [InlineData("Teknoloji", "Teknoloji")]
        [InlineData("Sanayi/Üretim", "Sanayi/Üretim")]
        [InlineData("Ulaştırma/Lojistik", "Ulaştırma/Lojistik")]
        [InlineData("Tarım/Gıda", "Tarım/Gıda")]
        [InlineData("Holding/Yatırım", "Holding/Yatırım")]
        [InlineData("Aracı Kurum/Portföy Yönetimi", "Aracı Kurum/Portföy Yönetimi")]
        [InlineData("Kamu/Diğer", "Kamu/Diğer")]
        [InlineData("Diğer", "Diğer")]
        public void Normalize_ExactMatch_ReturnsKanonikForm(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Normalize - Case Insensitivity

        [Theory]
        [InlineData("bankacılık", "Bankacılık")]
        [InlineData("BANKACILIK", "Bankacılık")]
        [InlineData("BaNkAcIlIk", "Bankacılık")]
        [InlineData("sigorta", "Sigorta")]
        [InlineData("SIGORTA", "Sigorta")]
        public void Normalize_DifferentCase_ReturnsKanonikForm(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Normalize - Turkish Character Normalization

        [Theory]
        [InlineData("Diger", "Diğer")]
        [InlineData("DIGER", "Diğer")]
        [InlineData("diger", "Diğer")]
        [InlineData("Dıger", "Diğer")]
        [InlineData("dıger", "Diğer")]
        public void Normalize_TurkishCharacters_NormalizesCorrectly(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Gayrimenkul (Gyo)", "Gayrimenkul (GYO)")]
        [InlineData("gayrimenkul (gyo)", "Gayrimenkul (GYO)")]
        public void Normalize_TurkishI_NormalizesCorrectly(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Sanayi/Uretim", "Sanayi/Üretim")]
        [InlineData("Sanayı/Üretim", "Sanayi/Üretim")]
        public void Normalize_OtherTurkishCharacters_NormalizesCorrectly(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Normalize - Whitespace Handling

        [Theory]
        [InlineData(" Bankacılık", "Bankacılık")]
        [InlineData("Bankacılık ", "Bankacılık")]
        [InlineData(" Bankacılık ", "Bankacılık")]
        [InlineData("  Bankacılık  ", "Bankacılık")]
        public void Normalize_LeadingTrailingWhitespace_TrimsCorrectly(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Aracı  Kurum/Portföy  Yönetimi", "Aracı Kurum/Portföy Yönetimi")]
        [InlineData("Araçı   Kurum/Portföy   Yönetimi", "Aracı Kurum/Portföy Yönetimi")]
        public void Normalize_MultipleInternalSpaces_NormalizesCorrectly(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Normalize - Null and Empty

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_NullOrEmpty_ReturnsDigerDefault(string? input)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal("Diğer", result);
        }

        #endregion

        #region Normalize - Unknown Sectors

        [Theory]
        [InlineData("UnknownSector")]
        [InlineData("Bilinmeyen Sektör")]
        [InlineData("XYZ")]
        [InlineData("123")]
        public void Normalize_UnknownSector_ReturnsDiger(string input)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal("Diğer", result);
        }

        #endregion

        #region Normalize - Special Cases

        [Fact]
        public void Normalize_AllKanonikSektors_ReturnThemselves()
        {
            // Arrange & Act & Assert
            foreach (var sektor in SektorNormalizer.KanonikSektorler)
            {
                var result = SektorNormalizer.Normalize(sektor);
                Assert.Equal(sektor, result);
            }
        }

        [Theory]
        [InlineData("bankacılık", "Bankacılık")]
        [InlineData("sigorta ", "Sigorta")]
        [InlineData(" enerji", "Enerji")]
        [InlineData("  holding/yatırım  ", "Holding/Yatırım")]
        public void Normalize_RealWorldExamples_NormalizesCorrectly(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Normalize_HyphenVsSlash_DistinguishesCorrectly()
        {
            // Arrange & Act
            var withSlash = SektorNormalizer.Normalize("Sanayi/Üretim");
            var unknown = SektorNormalizer.Normalize("Sanayi-Üretim");

            // Assert
            Assert.Equal("Sanayi/Üretim", withSlash);
            Assert.Equal("Diğer", unknown);
        }

        [Fact]
        public void Normalize_Consistency_SameInputAlwaysReturnsSameOutput()
        {
            // Arrange
            var input = "  bAnKaCıLıK  ";

            // Act
            var result1 = SektorNormalizer.Normalize(input);
            var result2 = SektorNormalizer.Normalize(input);
            var result3 = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
            Assert.Equal("Bankacılık", result1);
        }

        #endregion

        #region Normalize - Variations

        [Theory]
        [InlineData("Diger", "Diğer")]
        [InlineData("Diğer", "Diğer")]
        [InlineData("DiĞer", "Diğer")]
        [InlineData("DIĞEr", "Diğer")]
        public void Normalize_DigerVariations_AllNormalize(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Ulastirma/Lojistik", "Ulaştırma/Lojistik")]
        [InlineData("Ulaştırma/Lojistik", "Ulaştırma/Lojistik")]
        public void Normalize_TransportLogistics_AllVariationsNormalize(string input, string expected)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region KanonikSektorler Property

        [Fact]
        public void KanonikSektorler_ContainsExpectedSectors()
        {
            // Assert
            Assert.Contains("Bankacılık", SektorNormalizer.KanonikSektorler);
            Assert.Contains("Sigorta", SektorNormalizer.KanonikSektorler);
            Assert.Contains("Enerji", SektorNormalizer.KanonikSektorler);
            Assert.Contains("Diğer", SektorNormalizer.KanonikSektorler);
        }

        [Fact]
        public void KanonikSektorler_CountIsCorrect()
        {
            // Assert - Should have specific count of canonical sectors
            Assert.Equal(13, SektorNormalizer.KanonikSektorler.Length);
        }

        [Fact]
        public void KanonikSektorler_NoDuplicates()
        {
            // Act
            var uniqueSectors = SektorNormalizer.KanonikSektorler.Distinct().ToList();

            // Assert
            Assert.Equal(SektorNormalizer.KanonikSektorler.Length, uniqueSectors.Count);
        }

        #endregion

        #region Edge Cases

        [Theory]
        [InlineData("Bankacılık123")]
        [InlineData("123Bankacılık")]
        [InlineData("Ban@kacılık")]
        public void Normalize_WithNumbers_ReturnsDigerIfNotExact(string input)
        {
            // Act
            var result = SektorNormalizer.Normalize(input);

            // Assert
            Assert.Equal("Diğer", result);
        }

        [Fact]
        public void Normalize_VeryLongString_ReturnsDiger()
        {
            // Arrange
            var longString = new string('A', 10000);

            // Act
            var result = SektorNormalizer.Normalize(longString);

            // Assert
            Assert.Equal("Diğer", result);
        }

        [Fact]
        public void Normalize_OnlyWhitespace_ReturnsDiger()
        {
            // Act
            var result = SektorNormalizer.Normalize("       ");

            // Assert
            Assert.Equal("Diğer", result);
        }

        #endregion
    }
}
