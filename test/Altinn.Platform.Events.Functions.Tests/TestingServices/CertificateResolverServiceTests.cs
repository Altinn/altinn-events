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
            var keyVaultSettings = Options.Create(new KeyVaultSettings { KeyVaultURI = "https://altinn-at21-kv.vault.azure.net", PlatformCertSecretId = "platform-cert" });
            var certificateResolverService = new CertificateResolverService(loggerMock.Object, certificateResolverSettings, keyVaultServiceMock.Object, keyVaultSettings);

            var certBase64 = "MIIF1jCCA76gAwIBAgIQe/UpvwBNvG5aCRa+6QEZqzANBgkqhkiG9w0BAQsFADANMQswCQYDVQQDEwJjYTAeFw0xODAzMTcxODM5NTFaFw0yMDAzMTYxODM5NTFaMBQxEjAQBgNVBAMTCWFwaXNlcnZlcjCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAMWTr7hiQPr1csDR/OpAbR/tkNGN4mCOPlQx+xao+JQlPP1Uo66X9oc3vrCBPZG19FWodU5zRe2+ql279zmMLgwKIGOsYuDmAV7m9Gg9tNG5nB9WHhD7WX0gOBpPW0csJTDzlr9FkJcmMKXNFOYeydhkn2lrr+0uCumI4AC3j/ACRls7J7EV1Q09HyHdL+5q4eAr92AUs217PpWWAMqMFo4WC/4NuqEfnMR2jpPzDoIJBDxt3NljiaRRS2LfB34O4aCKCis2jlMjYTahKIXDDv1pWW67AsGuGBpgdb2iYRUMze4NIUqQZrVGhnDcnRcbsfsldbBEoZBfLEaUm0hgSJUNnX3K1Adv7lHGxbdk/m9M1YEjb1EAX7rKMPg2uKcHeVv74Xwa0+cvke8ErYg3iSuuLPQ4qzPTV6LcdCmfbBsIyUiVJpCaa4RX8uzRXHAx1EvN2k7iZQutdrT2Sgj+4cG9E33hZM2AsOJuyXZMMVMtUveOQeth8iQNcT4FDwqc1WZDnpVMlqpTnDzTIAlrN+5WzJgzIj6GTsILyKC91GuI5jSrCExjwUB6D4oGPA5X/eOiNU1yUFNouYSCnAun9D+RSVfBWEVAVumCRbRcsOmBIE6MwpCZCHzXhAUBvMzk9/qhVDxTuaBt+Nf4WHopAS0KFsJGVee1tsva4zI34HgLAgMBAAGjggEpMIIBJTAOBgNVHQ8BAf8EBAMCBaAwEwYDVR0lBAwwCgYIKwYBBQUHAwEwDAYDVR0TAQH/BAIwADCB7wYDVR0RBIHnMIHkgg5oY3Ata3ViZXJuZXRlc4IKa3ViZXJuZXRlc4IWa3ViZXJuZXRlcy5kZWZhdWx0LnN2Y4Ika3ViZXJuZXRlcy5kZWZhdWx0LnN2Yy5jbHVzdGVyLmxvY2FsgjloY3Ata3ViZXJuZXRlcy41YWFkNjBmMTg5YjU0NTAwMDEyYTQ0MDEuc3ZjLmNsdXN0ZXIubG9jYWyCQXNhYXJzLWFrcy0tc2FhcnMtcGxheS1yZXNvdS1lOGVhNGUtNGFkNmQyOTMuaGNwLndlc3R1czIuYXptazhzLmlvhwQKAAABhwQKAAABMA0GCSqGSIb3DQEBCwUAA4ICAQDHG4mm3iIOxzirvNX9SZn0G26Zt/h4z3k07mMKUHB9jmYbtqWqQX1LfocZs+s6/02q88ilwATFJg1Qv5NkW7QsfreSCbyOq/9JLMEiQlbddjkt/U8czUU0kGLn+0m758XkPkwRgPIiMz437YhlfmpVI5gv63QfxfnRqrK2WqmoO6RMmaWc2aZFoVL521KxX0pp+3vAE9AwfvWpNgJkTirVgNhe6QL1tfA0RVllGfil3Re1yAQaBYD3mIBtiFvTML/Zm3GjxJXtXqT7JtM4bibHqhKywjgx1rcDa1WOLta51mfGiqOMOP/sdXtKcs/zdIMZOie6mOh8ZNfHdGOdCrNbTj8fL3OtwlzJGFPuWwAYJjT8Fcudg6zCZ6CuK26tz3rJ7665NXVdS+ljAA2Pfl6MefhhYL4RUSWEtFCqNqeWgyRzWvQcVasTX7k8lptY8yLPO3c636UMvfESFQqVZpC6xv66c5jBarKeCUmRCjmtXqVgGtEQCDk7hVp1A9nxmpi4S0Ubg4bQAPIdkQeR4uj2Jiwu5a4sKQHV1LxDovWde15CuofMvzIswPJfMdM5TiOFGtd6vhFjcOGCvM370IrLS/tg8+vNuocx+orueX7vjHwYL3IBlrZctiRAAOklVoQfVNH/aY0cfbSvqTX3edTtT/h7GJuzVtfccpCvyw5pnw==";
            var x509Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Convert.FromBase64String(certBase64));
            keyVaultServiceMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(certBase64);

            // Act
            var result = await certificateResolverService.GetCertificateAsync();

            // Assert
            Assert.NotNull(result);
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
