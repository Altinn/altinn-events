﻿using System.Threading.Tasks;

using Altinn.Platform.Events.Models;

namespace Altinn.Platform.Events.Services.Interfaces
{
    /// <summary>
    /// Defines the methods required for an implementation of the push service.
    /// </summary>
    public interface IOutboundService
    {
        /// <summary>
        /// Deliver outbound cloudEvent to each matching subscriber.
        /// </summary>
        Task PostOutbound(CloudEvent cloudEvent);
    }
}