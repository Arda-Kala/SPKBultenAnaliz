using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SPKBultenAnaliz.Business.Configuration;
using SPKBultenAnaliz.Business.Security;
using SPKBultenAnaliz.Business.Services;
using SPKBultenAnaliz.Core.DTOs;
using SPKBultenAnaliz.Core.Entities;
using SPKBultenAnaliz.Core.Interfaces;
using Xunit;

namespace SPKBultenAnaliz.Tests
{
    /// <summary>
    /// AuthService birim testleri. Kullanıcı doğrulama, JWT token üretimi,
    /// kullanıcı yönetimi (ekleme/silme/şifre değiştirme) işlemlerini test eder.
    /// Moq kullanarak repository bağımlılıklarını taklit ederiz.
    /// </summary>
    public class AuthServiceTests
    {
        private readonly Mock<IKullaniciRepository> _repositoryMock;
        private readonly IOptions<JwtOptions> _jwtOptions;
        private readonly AuthService _sut;

        public AuthServiceTests()
        {
            _repositoryMock = new Mock<IKullaniciRepository>();
            _jwtOptions = Options.Create(new JwtOptions
            {
                Key = "this-is-a-test-secret-key-that-is-very-long-and-valid-for-256-bits",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                SureDakika = 60
            });

            _sut = new AuthService(
                _repositoryMock.Object,
                _jwtOptions,
                NullLogger<AuthService>.Instance);
        }

        #region GirisYapAsync Tests

        [Fact]
        public async Task GirisYapAsync_ValidCredentials_ReturnsGirisResponse()
        {
            // Arrange
            var kullaniciAdi = "testuser";
            var sifre = "TestPassword123";
            var sifreHash = SifreHashService.Hashle(sifre);

            var kullanici = new Kullanici
            {
                Id = 1,
                KullaniciAdi = kullaniciAdi,
                SifreHash = sifreHash,
                Rol = "Admin",
                Aktif = true,
                SonGirisTarihi = null
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(kullaniciAdi))
                .ReturnsAsync(kullanici);

            // Act
            var result = await _sut.GirisYapAsync(kullaniciAdi, sifre);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(kullaniciAdi, result.KullaniciAdi);
            Assert.Equal("Admin", result.Rol);
            Assert.NotEmpty(result.Token);
            Assert.True(result.SonaErmeTarihi > DateTime.UtcNow);
            _repositoryMock.Verify(r => r.GuncelleAsync(It.Is<Kullanici>(k => k.Id == 1)), Times.Once);
        }

        [Fact]
        public async Task GirisYapAsync_InvalidPassword_ReturnsNull()
        {
            // Arrange
            var kullaniciAdi = "testuser";
            var dogruSifre = "TestPassword123";
            var yanlisSifre = "WrongPassword123";

            var kullanici = new Kullanici
            {
                Id = 1,
                KullaniciAdi = kullaniciAdi,
                SifreHash = SifreHashService.Hashle(dogruSifre),
                Rol = "Admin",
                Aktif = true
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(kullaniciAdi))
                .ReturnsAsync(kullanici);

            // Act
            var result = await _sut.GirisYapAsync(kullaniciAdi, yanlisSifre);

            // Assert
            Assert.Null(result);
            _repositoryMock.Verify(r => r.GuncelleAsync(It.IsAny<Kullanici>()), Times.Never);
        }

        [Fact]
        public async Task GirisYapAsync_UserNotFound_ReturnsNull()
        {
            // Arrange
            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(It.IsAny<string>()))
                .ReturnsAsync((Kullanici)null!);

            // Act
            var result = await _sut.GirisYapAsync("nonexistent", "anypassword");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GirisYapAsync_InactiveUser_ReturnsNull()
        {
            // Arrange
            var kullaniciAdi = "inactiveuser";
            var kullanici = new Kullanici
            {
                Id = 1,
                KullaniciAdi = kullaniciAdi,
                SifreHash = SifreHashService.Hashle("ValidPassword123"),
                Rol = "Admin",
                Aktif = false // Pasif kullanıcı
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(kullaniciAdi))
                .ReturnsAsync(kullanici);

            // Act
            var result = await _sut.GirisYapAsync(kullaniciAdi, "ValidPassword123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GirisYapAsync_UpdatesLastLoginTime()
        {
            // Arrange
            var kullaniciAdi = "testuser";
            var sifre = "TestPassword123";

            var kullanici = new Kullanici
            {
                Id = 1,
                KullaniciAdi = kullaniciAdi,
                SifreHash = SifreHashService.Hashle(sifre),
                Rol = "User",
                Aktif = true,
                SonGirisTarihi = null
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(kullaniciAdi))
                .ReturnsAsync(kullanici);

            var beforeLogin = DateTime.UtcNow;

            // Act
            await _sut.GirisYapAsync(kullaniciAdi, sifre);

            // Assert
            _repositoryMock.Verify(
                r => r.GuncelleAsync(It.Is<Kullanici>(k =>
                    k.SonGirisTarihi >= beforeLogin && k.SonGirisTarihi <= DateTime.UtcNow)),
                Times.Once);
        }

        #endregion

        #region KullaniciEkleAsync Tests

        [Fact]
        public async Task KullaniciEkleAsync_ValidRequest_CreatesUser()
        {
            // Arrange
            var istek = new KullaniciEkleRequest
            {
                KullaniciAdi = "newuser",
                Sifre = "ValidPassword123",
                Rol = "Editor"
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(istek.KullaniciAdi.Trim()))
                .ReturnsAsync((Kullanici)null!);

            Kullanici? savedKullanici = null;
            _repositoryMock.Setup(r => r.EkleAsync(It.IsAny<Kullanici>()))
                .Callback<Kullanici>(k => savedKullanici = k)
                .ReturnsAsync((Kullanici k) => { k.Id = 1; return k; });

            // Act
            var result = await _sut.KullaniciEkleAsync(istek);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("newuser", result.KullaniciAdi);
            Assert.Equal("Editor", result.Rol);
            _repositoryMock.Verify(r => r.EkleAsync(It.IsAny<Kullanici>()), Times.Once);
        }

        [Fact]
        public async Task KullaniciEkleAsync_DuplicateUsername_ThrowsException()
        {
            // Arrange
            var istek = new KullaniciEkleRequest
            {
                KullaniciAdi = "existinguser",
                Sifre = "ValidPassword123"
            };

            var existingKullanici = new Kullanici
            {
                Id = 1,
                KullaniciAdi = "existinguser",
                SifreHash = "hash",
                Rol = "Admin",
                Aktif = true
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(istek.KullaniciAdi.Trim()))
                .ReturnsAsync(existingKullanici);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.KullaniciEkleAsync(istek));
            Assert.Contains("zaten kullanılıyor", ex.Message);
        }

        [Fact]
        public async Task KullaniciEkleAsync_UsernameShort_ThrowsException()
        {
            // Arrange
            var istek = new KullaniciEkleRequest
            {
                KullaniciAdi = "ab", // 2 karakter - minimum 3 gerekli
                Sifre = "ValidPassword123"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.KullaniciEkleAsync(istek));
            Assert.Contains("3 karakter", ex.Message);
        }

        [Fact]
        public async Task KullaniciEkleAsync_PasswordShort_ThrowsException()
        {
            // Arrange
            var istek = new KullaniciEkleRequest
            {
                KullaniciAdi = "validuser",
                Sifre = "short" // 5 karakter - minimum 6 gerekli
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(It.IsAny<string>()))
                .ReturnsAsync((Kullanici)null!);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.KullaniciEkleAsync(istek));
            Assert.Contains("6 karakter", ex.Message);
        }

        [Fact]
        public async Task KullaniciEkleAsync_DefaultRole_SetsAdminWhenNotProvided()
        {
            // Arrange
            var istek = new KullaniciEkleRequest
            {
                KullaniciAdi = "newuser",
                Sifre = "ValidPassword123"
                // Rol belirtilmemiş
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync(istek.KullaniciAdi.Trim()))
                .ReturnsAsync((Kullanici)null!);

            _repositoryMock.Setup(r => r.EkleAsync(It.IsAny<Kullanici>()))
                .ReturnsAsync((Kullanici k) => { k.Id = 1; return k; });

            // Act
            var result = await _sut.KullaniciEkleAsync(istek);

            // Assert
            Assert.Equal("Admin", result.Rol);
        }

        [Fact]
        public async Task KullaniciEkleAsync_TrimsUsername()
        {
            // Arrange
            var istek = new KullaniciEkleRequest
            {
                KullaniciAdi = "  newuser  ",
                Sifre = "ValidPassword123"
            };

            _repositoryMock.Setup(r => r.KullaniciAdiIleGetirAsync("newuser"))
                .ReturnsAsync((Kullanici)null!);

            _repositoryMock.Setup(r => r.EkleAsync(It.IsAny<Kullanici>()))
                .ReturnsAsync((Kullanici k) => { k.Id = 1; return k; });

            // Act
            var result = await _sut.KullaniciEkleAsync(istek);

            // Assert
            Assert.Equal("newuser", result.KullaniciAdi);
        }

        #endregion

        #region SifreDegistirAsync Tests

        [Fact]
        public async Task SifreDegistirAsync_ValidOldPassword_ChangesPassword()
        {
            // Arrange
            var kullaniciId = 1;
            var eskiSifre = "OldPassword123";
            var yeniSifre = "NewPassword123";

            var kullanici = new Kullanici
            {
                Id = kullaniciId,
                KullaniciAdi = "testuser",
                SifreHash = SifreHashService.Hashle(eskiSifre),
                Rol = "Admin",
                Aktif = true
            };

            _repositoryMock.Setup(r => r.IdIleGetirAsync(kullaniciId))
                .ReturnsAsync(kullanici);

            // Act
            await _sut.SifreDegistirAsync(kullaniciId, eskiSifre, yeniSifre);

            // Assert
            _repositoryMock.Verify(
                r => r.GuncelleAsync(It.Is<Kullanici>(k =>
                    SifreHashService.Dogrula(yeniSifre, k.SifreHash))),
                Times.Once);
        }

        [Fact]
        public async Task SifreDegistirAsync_InvalidOldPassword_ThrowsException()
        {
            // Arrange
            var kullaniciId = 1;
            var kullanici = new Kullanici
            {
                Id = kullaniciId,
                KullaniciAdi = "testuser",
                SifreHash = SifreHashService.Hashle("CorrectPassword123"),
                Rol = "Admin",
                Aktif = true
            };

            _repositoryMock.Setup(r => r.IdIleGetirAsync(kullaniciId))
                .ReturnsAsync(kullanici);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.SifreDegistirAsync(kullaniciId, "WrongPassword", "NewPassword123"));
            Assert.Contains("hatalı", ex.Message);
        }

        [Fact]
        public async Task SifreDegistirAsync_UserNotFound_ThrowsException()
        {
            // Arrange
            _repositoryMock.Setup(r => r.IdIleGetirAsync(It.IsAny<int>()))
                .ReturnsAsync((Kullanici)null!);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.SifreDegistirAsync(999, "OldPassword", "NewPassword"));
            Assert.Contains("bulunamadı", ex.Message);
        }

        [Fact]
        public async Task SifreDegistirAsync_NewPasswordTooShort_ThrowsException()
        {
            // Arrange
            var kullaniciId = 1;
            var kullanici = new Kullanici
            {
                Id = kullaniciId,
                KullaniciAdi = "testuser",
                SifreHash = SifreHashService.Hashle("OldPassword123"),
                Rol = "Admin",
                Aktif = true
            };

            _repositoryMock.Setup(r => r.IdIleGetirAsync(kullaniciId))
                .ReturnsAsync(kullanici);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => _sut.SifreDegistirAsync(kullaniciId, "OldPassword123", "short"));
            Assert.Contains("6 karakter", ex.Message);
        }

        #endregion

        #region KullaniciSilAsync Tests

        [Fact]
        public async Task KullaniciSilAsync_DifferentUser_DeletesSuccessfully()
        {
            // Arrange
            var currentUserId = 1;
            var userToDeleteId = 2;

            _repositoryMock.Setup(r => r.AktifKullaniciSayisiAsync())
                .ReturnsAsync(3); // En az 2 kullanıcı var

            // Act
            // NOT: Gerçek imza KullaniciSilAsync(kullaniciId, silenKullaniciId) şeklindedir —
            // yani ilk parametre SİLİNECEK kullanıcı, ikincisi İŞLEMİ YAPAN kullanıcıdır.
            await _sut.KullaniciSilAsync(userToDeleteId, currentUserId);

            // Assert
            _repositoryMock.Verify(r => r.SilAsync(userToDeleteId), Times.Once);
        }

        [Fact]
        public async Task KullaniciSilAsync_SelfDelete_ThrowsException()
        {
            // Arrange & Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.KullaniciSilAsync(1, 1));
            Assert.Contains("silemezsiniz", ex.Message);
        }

        [Fact]
        public async Task KullaniciSilAsync_OnlyUserLeft_ThrowsException()
        {
            // Arrange
            _repositoryMock.Setup(r => r.AktifKullaniciSayisiAsync())
                .ReturnsAsync(1); // Sadece 1 kullanıcı

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.KullaniciSilAsync(1, 2));
            Assert.Contains("son kullanıcı", ex.Message);
        }

        #endregion

        #region TumKullanicilariGetirAsync Tests

        [Fact]
        public async Task TumKullanicilariGetirAsync_ReturnsAllUsers()
        {
            // Arrange
            var kullanicilar = new List<Kullanici>
            {
                new() { Id = 1, KullaniciAdi = "user1", SifreHash = "hash", Rol = "Admin", Aktif = true, OlusturmaTarihi = DateTime.UtcNow },
                new() { Id = 2, KullaniciAdi = "user2", SifreHash = "hash", Rol = "Editor", Aktif = true, OlusturmaTarihi = DateTime.UtcNow }
            };

            _repositoryMock.Setup(r => r.TumunuGetirAsync())
                .ReturnsAsync(kullanicilar);

            // Act
            var result = await _sut.TumKullanicilariGetirAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, k => Assert.NotNull(k.KullaniciAdi));
        }

        #endregion
    }
}
