using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Platform.Events.Configuration;
using Altinn.Platform.Events.Exceptions;
using Altinn.Platform.Events.Services;
using Altinn.Platform.Events.Tests.Mocks;
using Altinn.Platform.Events.Tests.Utils;
using Altinn.Platform.Register.Enums;
using Altinn.Platform.Register.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;

using Xunit;

namespace Altinn.Platform.Events.Tests.TestingServices
{
    public class RegisterServiceTest
    {
        private readonly Mock<IOptions<PlatformSettings>> _platformSettings;
        private readonly Mock<IOptions<GeneralSettings>> _generalSettings;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly Mock<IHttpContextAccessor> _contextAccessor;
        private readonly Mock<IAccessTokenGenerator> _accessTokenGenerator;

        public RegisterServiceTest()
        {
            _platformSettings = new Mock<IOptions<PlatformSettings>>();
            _generalSettings = new Mock<IOptions<GeneralSettings>>();
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _contextAccessor = new Mock<IHttpContextAccessor>();
            _accessTokenGenerator = new Mock<IAccessTokenGenerator>();
        }

        [Fact]
        public async Task PartyLookup_MatchFound_IdReturned()
        {
            // Arrange
            Party party = new Party
            {
                PartyId = 500000,
                OrgNumber = "897069650",
                PartyTypeName = PartyType.Organisation
            };
            int expected = 500000;
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(party), Encoding.UTF8, "application/json")
            };

            HttpRequestMessage actualRequest = null;
            void SetRequest(HttpRequestMessage request) => actualRequest = request;
            InitializeMocks(httpResponseMessage, SetRequest);

            HttpClient httpClient = new HttpClient(_handlerMock.Object);

            RegisterService target = new RegisterService(
                httpClient,
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            // Act
            int actual = await target.PartyLookup("897069650", null);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task PartyLookup_ResponseIsNotSuccessful_PlatformExceptionThrown()
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

            HttpClient httpClient = new HttpClient(_handlerMock.Object);

            RegisterService target = new RegisterService(
                httpClient,
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            // Act & Assert
            await Assert.ThrowsAsync<PlatformHttpException>(async () => { await target.PartyLookup("16069412345", null); });
        }

        [Fact]
        public async Task PartyLookup_QueryWithTwoPersons_PerformsSingleRequestToRegister()
        {
            // Arrange
            HttpRequestMessage requestMessage = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                requestMessage = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(await TestDataLoader.Load<PartiesRegisterQueryResponse>("twopersons"))
                };
            });

            InitializeMocks(5);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            List<string> partyUrnList = [
                "urn:altinn:person:identifier-no:02056241046", 
                "urn:altinn:person:identifier-no:31073102351"];

            // Act
            List<PartyIdentifiers> partyIdentifiers = [.. await target.PartyLookup(partyUrnList, CancellationToken.None)];

            // Assert
            Assert.NotNull(requestMessage);
            Assert.Equal(HttpMethod.Post, requestMessage.Method);
            const string ExpectedRequestUri = 
                "http://localhost:5101/register/api/v2/internal/parties/query?fields=identifiers";
            Assert.Equal(ExpectedRequestUri, requestMessage.RequestUri.ToString());

            var requestContent = await requestMessage.Content.ReadFromJsonAsync<PartiesRegisterQueryRequest>();
            Assert.NotNull(requestContent);
            Assert.Equal(2, requestContent.Data.Length);

            Assert.NotNull(partyIdentifiers);
            Assert.Equal("02056241046", partyIdentifiers[0].PersonIdentifier);
        }

        [Fact]
        public async Task PartyLookup_QueryWithMorePersonsThanSupported_PerformsTwoRequestsToRegister()
        {
            // Arrange
            HttpRequestMessage firstRequestMessage = null;
            HttpRequestMessage secondRequestMessage = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                if (firstRequestMessage is null)
                {
                    firstRequestMessage = request;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(await TestDataLoader.Load<PartiesRegisterQueryResponse>("twopersons"))
                    };
                }
                else
                {
                    secondRequestMessage = request;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(await TestDataLoader.Load<PartiesRegisterQueryResponse>("oneperson"))
                    };
                }
            });
            
            InitializeMocks(2);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            List<string> partyUrnList = [
                "urn:altinn:person:identifier-no:02056241046",
                "urn:altinn:person:identifier-no:31073102351",
                "urn:altinn:person:identifier-no:18874198354"];

            // Act
            List<PartyIdentifiers> partyIdentifiers = [.. await target.PartyLookup(partyUrnList, CancellationToken.None)];

            // Assert
            const string ExpectedRequestUri =
                "http://localhost:5101/register/api/v2/internal/parties/query?fields=identifiers";

            Assert.NotNull(firstRequestMessage);
            Assert.Equal(HttpMethod.Post, firstRequestMessage.Method);
            Assert.Equal(ExpectedRequestUri, firstRequestMessage.RequestUri.ToString());

            var firstRequestContent = await firstRequestMessage.Content.ReadFromJsonAsync<PartiesRegisterQueryRequest>();
            Assert.NotNull(firstRequestContent);
            Assert.Equal(2, firstRequestContent.Data.Length);

            Assert.NotNull(secondRequestMessage);
            Assert.Equal(HttpMethod.Post, secondRequestMessage.Method);
            Assert.Equal(ExpectedRequestUri, secondRequestMessage.RequestUri.ToString());

            var secondRequestContent = await secondRequestMessage.Content.ReadFromJsonAsync<PartiesRegisterQueryRequest>();
            Assert.NotNull(secondRequestContent);
            Assert.Single(secondRequestContent.Data);
        }

        [Fact]
        public async Task PartyLookup_QueryResponsIsBadRequest_ThrowsPlatformException()
        {
            // Arrange
            HttpRequestMessage requestMessage = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                await Task.CompletedTask;
                requestMessage = request;
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            });

            InitializeMocks(10);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            List<string> partyUrnList = [
                "urn:altinn:person:identifier-no:02056241046"];

            PlatformHttpException actualException = null;

            // Act
            try
            {
                _ = await target.PartyLookup(partyUrnList, CancellationToken.None);
            }
            catch (PlatformHttpException phe)
            {
                actualException = phe;
            }

            // Assert
            const string ExpectedRequestUri =
                "http://localhost:5101/register/api/v2/internal/parties/query?fields=identifiers";

            Assert.NotNull(requestMessage);
            Assert.Equal(HttpMethod.Post, requestMessage.Method);
            Assert.Equal(ExpectedRequestUri, requestMessage.RequestUri.ToString());

            Assert.NotNull(actualException);
            Assert.Equal(HttpStatusCode.BadRequest, actualException.Response.StatusCode);
        }

        [Fact]
        public async Task GetMainUnit_MainUnitFound_ReturnsOrganizationRecord()
        {
            // Arrange
            var expectedRecord = new OrganizationRecord
            {
                OrganizationIdentifier = "311443755",
                ExternalUrn = "urn:altinn:organization:identifier-no:311443755",
                PartyId = 51326197
            };

            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                await Task.CompletedTask;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new LookupMainUnitResponse { Data = [expectedRecord] })
                };
            });

            InitializeMocks(10);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            // Act
            OrganizationRecord actual = await target.GetMainUnit(
                "urn:altinn:party:id:51326197", CancellationToken.None);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expectedRecord.OrganizationIdentifier, actual.OrganizationIdentifier);
            Assert.Equal(expectedRecord.ExternalUrn, actual.ExternalUrn);
            Assert.Equal(expectedRecord.PartyId, actual.PartyId);
        }

        [Fact]
        public async Task GetMainUnit_EmptyDataList_ReturnsNull()
        {
            // Arrange
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                await Task.CompletedTask;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new LookupMainUnitResponse())
                };
            });

            InitializeMocks(10);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            // Act
            OrganizationRecord actual = await target.GetMainUnit(
                "urn:altinn:organization:identifier-no:000000000", CancellationToken.None);

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public async Task GetMainUnit_MultipleResults_ReturnsFirstAndLogsWarning()
        {
            // Arrange
            var firstRecord = new OrganizationRecord { OrganizationIdentifier = "311443755", PartyId = 51326197 };
            var secondRecord = new OrganizationRecord { OrganizationIdentifier = "314249879", PartyId = 51326198 };

            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                await Task.CompletedTask;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new LookupMainUnitResponse { Data = [firstRecord, secondRecord] })
                };
            });

            InitializeMocks(10);

            var loggerMock = new Mock<ILogger<RegisterService>>();

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                loggerMock.Object);

            // Act
            OrganizationRecord actual = await target.GetMainUnit(
                "urn:altinn:party:id:51326197", CancellationToken.None);

            // Assert — first record returned
            Assert.NotNull(actual);
            Assert.Equal(firstRecord.OrganizationIdentifier, actual.OrganizationIdentifier);

            // Assert — warning was logged
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetMainUnit_ValidRequest_PostsToCorrectEndpointWithOrgUrn()
        {
            // Arrange
            const string OrgUrn = "urn:altinn:party:id:51326197";
            const string ExpectedRequestUri =
                "http://localhost:5101/register/api/v2/internal/parties/main-units";

            HttpRequestMessage requestMessage = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                requestMessage = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new LookupMainUnitResponse
                    {
                        Data = [new OrganizationRecord { OrganizationIdentifier = "311443755", PartyId = 50001337 }]
                    })
                };
            });

            InitializeMocks(10);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            // Act
            await target.GetMainUnit(OrgUrn, CancellationToken.None);

            // Assert — correct endpoint and body
            Assert.NotNull(requestMessage);
            Assert.Equal(HttpMethod.Post, requestMessage.Method);
            Assert.Equal(ExpectedRequestUri, requestMessage.RequestUri.ToString());

            var bodyJson = await requestMessage.Content.ReadAsStringAsync();
            Assert.Contains($"\"{OrgUrn}\"", bodyJson);
        }

        [Fact]
        public async Task GetMainUnit_NonSuccessfullRegisterRequest_ThrowsPlatformHttpException()
        {
            // Arrange
            const string OrgUrn = "urn:altinn:party:id:51326197";

            HttpRequestMessage requestMessage = null;
            DelegatingHandlerStub messageHandler = new(async (request, token) =>
            {
                requestMessage = request;
                return new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = JsonContent.Create(new LookupMainUnitResponse())
                };
            });

            InitializeMocks(10);

            RegisterService target = new(
                new HttpClient(messageHandler),
                _contextAccessor.Object,
                _accessTokenGenerator.Object,
                _generalSettings.Object,
                _platformSettings.Object,
                new Mock<ILogger<RegisterService>>().Object);

            // Act
            PlatformHttpException actual = null;
            try
            {
                await target.GetMainUnit(OrgUrn, CancellationToken.None);
            }
            catch (PlatformHttpException ex)
            {
                actual = ex;
            }

            // Assert
            Assert.NotNull(actual);
        }

        private void InitializeMocks(HttpResponseMessage httpResponseMessage, Action<HttpRequestMessage> callback)
        {
            InitializeMocks(10);

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => callback(request))
                .ReturnsAsync(httpResponseMessage)
                .Verifiable();
        }

        private void InitializeMocks(int chunkSize)
        {
            PlatformSettings platformSettings = new PlatformSettings
            {
                RegisterApiBaseAddress = "http://localhost:5101/register/api/",
                RegisterApiChunkSize = chunkSize
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
        }
    }
}
