using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Profile.Enums;
using Altinn.Platform.Profile.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    public class ProfileServiceTest
    {
        private readonly Mock<IOptions<PlatformSettings>> _platformSettings;
        private readonly Mock<IOptions<GeneralSettings>> _generalSettings;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly Mock<IHttpContextAccessor> _contextAccessor;
        private readonly Mock<IAccessTokenGenerator> _accessTokenGenerator;

        public ProfileServiceTest()
        {
            _platformSettings = new Mock<IOptions<PlatformSettings>>();
            _generalSettings = new Mock<IOptions<GeneralSettings>>();
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _contextAccessor = new Mock<IHttpContextAccessor>();
            _accessTokenGenerator = new Mock<IAccessTokenGenerator>();
        }

        [Fact]
        public async Task GetUserProfile_SuccessResponse_UserProfileReturned()
        {
            // Arrange
            UserProfile input = new()
            {
                UserId = 12345,
                UserType = UserType.SSNIdentified,
                Email = "testemail@automatedtest.com"
            };

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json")
            };

            HttpRequestMessage actualRequest = null;
            void SetRequest(HttpRequestMessage request) => actualRequest = request;
            InitializeMocks(httpResponseMessage, SetRequest);

            HttpClient httpClient = new(_handlerMock.Object);

            ProfileService target = new(
                _platformSettings.Object,
                new Mock<ILogger<ProfileService>>().Object,
                _contextAccessor.Object,
                httpClient,
                _accessTokenGenerator.Object,
                _generalSettings.Object);

            // Act
            UserProfile actual = await target.GetUserProfile(12345);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(UserType.SSNIdentified, actual.UserType);
            Assert.Equal("testemail@automatedtest.com", actual.Email);
        }

        [Fact]
        public async Task PartyLookup_ResponseIsNotSuccessful_NullReturned()
        {
            // Arrange
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent(string.Empty)
            };

            HttpRequestMessage actualRequest = null;
            void SetRequest(HttpRequestMessage request) => actualRequest = request;
            InitializeMocks(httpResponseMessage, SetRequest);

            HttpClient httpClient = new(_handlerMock.Object);

            ProfileService target = new(
                         _platformSettings.Object,
                         new Mock<ILogger<ProfileService>>().Object,
                         _contextAccessor.Object,
                         httpClient,
                         _accessTokenGenerator.Object,
                         _generalSettings.Object);

            // Act
            UserProfile actual = await target.GetUserProfile(12345);

            // Assert
            Assert.Null(actual);
        }

        private void InitializeMocks(HttpResponseMessage httpResponseMessage, Action<HttpRequestMessage> callback)
        {
            PlatformSettings platformSettings = new PlatformSettings
            {
                ApiProfileEndpoint = "http://localhost:5101/profile/api/v1/"
            };

            _platformSettings.Setup(s => s.Value).Returns(platformSettings);

            GeneralSettings generalSettings = new GeneralSettings
            {
                JwtCookieName = "AltinnStudioRuntime"
            };

            _generalSettings.Setup(s => s.Value).Returns(generalSettings);

            _contextAccessor.Setup(s => s.HttpContext).Returns(new DefaultHttpContext());

            _accessTokenGenerator.Setup(s => s.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(string.Empty);

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => callback(request))
                .ReturnsAsync(httpResponseMessage)
                .Verifiable();
        }
    }
}
