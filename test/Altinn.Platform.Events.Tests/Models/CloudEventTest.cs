using System;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using Altinn.Platform.Events.Models;

using Xunit;

namespace Altinn.Platform.Events.Tests.Models
{
    public class CloudEventTest
    {
        [Theory]
        [InlineData("{\"id\":\"689880e2-bb76-4dae-8500-de17bcd4b180\",\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50019855/428a4575-2c04-4400-89a3-1aaadd2579cd\",\"specversion\":\"1.0\",\"type\":\"app.instance.process.completed\",\"subject\":\"/party/50019855\",\"alternativesubject\":\"/person/16035001577\",\"time\": \"2022-05-12T00:02:11.260215+00\"}")]
        [InlineData("{\"id\":\"689880e2-bb76-4dae-8500-de17bcd4b180\",\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50019855/428a4575-2c04-4400-89a3-1aaadd2579cd\",\"specversion\":\"1.0\",\"type\":\"app.instance.process.completed\",\"subject\":\"/party/50019855\",\"time\": \"2022-05-12T00:02:11.260215+00\"}")]
        [InlineData("{\"id\":\"689880e2-bb76-4dae-8500-de17bcd4b180\",\"source\":\"urn:source:objectid\",\"specversion\":\"1.0\",\"type\":\"app.instance.process.completed\",\"subject\":\"/person/16069412345\",\"time\": \"2022-05-12T00:02:11.260215+00\"}")]
        public void ValidateRequiredProperties_ValidEvents_ReturnsTrue(string serializedEvent)
        {
            CloudEventOld cloudEvent = JsonSerializer.Deserialize<CloudEventOld>(serializedEvent);
            Assert.True(cloudEvent.ValidateRequiredProperties());
        }

        [Theory]
        [InlineData("{ \"source\": \"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/545111e6-4e2d-4366-a372-ad4cc9ce6450\", \"specversion\": \"1.0\", \"type\": \"app.instance.created\", \"subject\": \"/party/50002108\", \"alternativesubject\": \"/person/01014922047\", \"time\": \"2022-09-29T12:35:04.876702+00\"}")]
        [InlineData("{\"id\":\"386e9740-1747-4b89-b995-a01324f6d47d\",\"specversion\":\"1.0\",\"type\":\"app.instance.created\",\"subject\":\"/party/50002108\",\"alternativesubject\":\"/person/01014922047\",\"time\": \"2022-09-29T12:35:04.876702+00\"}")]
        [InlineData("{\"id\":\"386e9740-1747-4b89-b995-a01324f6d47d\",\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/545111e6-4e2d-4366-a372-ad4cc9ce6450\",\"type\":\"app.instance.created\",\"subject\":\"/party/50002108\",\"alternativesubject\":\"/person/01014922047\",\"time\": \"2022-09-29T12:35:04.876702+00\"}")]
        [InlineData("{\"id\":\"386e9740-1747-4b89-b995-a01324f6d47d\",\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/545111e6-4e2d-4366-a372-ad4cc9ce6450\",\"specversion\":\"1.0\",\"subject\":\"/party/50002108\",\"time\": \"2022-09-29T12:35:04.876702+00\"}")]
        [InlineData("{\"id\":\"386e9740-1747-4b89-b995-a01324f6d47d\",\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/545111e6-4e2d-4366-a372-ad4cc9ce6450\",\"specversion\":\"1.0\",\"type\":\"app.instance.created\",\"time\": \"2022-09-29T12:35:04.876702+00\"}")]
        [InlineData("{\"id\":\"386e9740-1747-4b89-b995-a01324f6d47d\",\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/545111e6-4e2d-4366-a372-ad4cc9ce6450\",\"specversion\":\"1.0\",\"type\":\"app.instance.created\",\"subject\":\"/party/50002108\"}")]
        [InlineData("{\"source\":\"https://ttd.apps.at22.altinn.cloud/ttd/apps-test/instances/50002108/545111e6-4e2d-4366-a372-ad4cc9ce6450\",\"specversion\":\"1.0\",\"type\":\"app.instance.created\",\"subject\":\"/party/50002108\"}")]
        public void ValidateRequiredProperties_InvalidEvents_ReturnsFalse(string serializedEvent)
        {
            CloudEventOld cloudEvent = JsonSerializer.Deserialize<CloudEventOld>(serializedEvent);
            Assert.False(cloudEvent.ValidateRequiredProperties());
        }
    }
}
