using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Mappers
{
    /// <summary>
    /// A cøass that holds the cloud event mapper configurations
    /// </summary>
    public class CloudEventMapper : AutoMapper.Profile
    {
        /// <summary>
        /// The cloud event mapper configuration
        /// </summary>
        public CloudEventMapper()
        {
            CreateMap<AppCloudEventRequestModel, CloudEvent>();
        }
    }
}
