using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.Services
{
    public class CertificateResolverServiceTests
    {
        [Fact]
        public async Task GetCertificateAsync_ShouldRetrieveCertificateFromKeyVault()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ICertificateResolverService>>();
            var certificateResolverSettings = Options.Create(new CertificateResolverSettings { CacheCertLifetimeInSeconds = 3600 }); // 1 hour cache lifetime
            var keyVaultServiceMock = new Mock<IKeyVaultService>();
            var keyVaultSettings = Options.Create(new KeyVaultSettings { KeyVaultURI = "https://example.vault.azure.net", PlatformCertSecretId = "platform-cert" });
            var certificateResolverService = new CertificateResolverService(loggerMock.Object, certificateResolverSettings, keyVaultServiceMock.Object, keyVaultSettings);

            var certBase64 = File.ReadAllLines(@$"../../../TestingServices/platform-org.pfx")[0];
            var x509Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Convert.FromBase64String(certBase64));
            keyVaultServiceMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(certBase64);

            // Act
            var result = await certificateResolverService.GetCertificateAsync();

            // Assert
            Assert.NotNull(result);
            keyVaultServiceMock.Verify(x => x.GetCertificateAsync(keyVaultSettings.Value.KeyVaultURI, keyVaultSettings.Value.PlatformCertSecretId), Times.Once);
        }

        [Fact]
        public async Task GetCertificateAsync_ShouldRetrieveCertificateFromCache()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ICertificateResolverService>>();
            var certificateResolverSettings = Options.Create(new CertificateResolverSettings { CacheCertLifetimeInSeconds = 3600 }); // 1 hour cache lifetime
            var keyVaultServiceMock = new Mock<IKeyVaultService>();
            var keyVaultSettings = Options.Create(new KeyVaultSettings { KeyVaultURI = "https://example.vault.azure.net", PlatformCertSecretId = "platform-cert" });
            var certificateResolverService = new CertificateResolverService(loggerMock.Object, certificateResolverSettings, keyVaultServiceMock.Object, keyVaultSettings);

            var certBase64 = File.ReadAllLines(@$"../../../TestingServices/platform-org.pfx")[0];
            var x509Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Convert.FromBase64String(certBase64));
            keyVaultServiceMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(certBase64);

            // Act
            var result1 = await certificateResolverService.GetCertificateAsync();
            var result2 = await certificateResolverService.GetCertificateAsync();

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            keyVaultServiceMock.Verify(x => x.GetCertificateAsync(keyVaultSettings.Value.KeyVaultURI, keyVaultSettings.Value.PlatformCertSecretId), Times.Once);
        }

        [Fact]
        public async Task GetCertificateAsync_ShouldFailWhenCertificateFormatIsInvalid()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ICertificateResolverService>>();
            var certificateResolverSettings = Options.Create(new CertificateResolverSettings { CacheCertLifetimeInSeconds = 3600 }); // 1 hour cache lifetime
            var keyVaultServiceMock = new Mock<IKeyVaultService>();
            var keyVaultSettings = Options.Create(new KeyVaultSettings { KeyVaultURI = "https://altinn-at21-kv.vault.azure.net", PlatformCertSecretId = "platform-cert" });
            var certificateResolverService = new CertificateResolverService(loggerMock.Object, certificateResolverSettings, keyVaultServiceMock.Object, keyVaultSettings);

            // Simulate invalid certificate data (non-base64 encoded)
            var invalidCertBase64 = "invalid_certificate_data";
            keyVaultServiceMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(invalidCertBase64);

            // Act and Assert
            var exception = await Assert.ThrowsAsync<FormatException>(() => certificateResolverService.GetCertificateAsync());
            Assert.Contains("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.", exception.Message);
        }
    }
}
