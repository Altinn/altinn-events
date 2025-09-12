using System.Security.Cryptography.X509Certificates;
using Altinn.Platform.Events.Functions.Configuration;
using Altinn.Platform.Events.Functions.Services;
using Altinn.Platform.Events.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Altinn.Platform.Events.Functions.Tests.TestingServices;

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
        var x509Certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certBase64));
        keyVaultServiceMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(x509Certificate);

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
        var x509Certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certBase64));
        keyVaultServiceMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(x509Certificate);

        // Act
        var result1 = await certificateResolverService.GetCertificateAsync();
        var result2 = await certificateResolverService.GetCertificateAsync();

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        keyVaultServiceMock.Verify(x => x.GetCertificateAsync(keyVaultSettings.Value.KeyVaultURI, keyVaultSettings.Value.PlatformCertSecretId), Times.Once);
    }
}
