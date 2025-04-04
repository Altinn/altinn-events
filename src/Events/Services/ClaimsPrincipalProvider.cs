﻿using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Altinn.Platform.Events.Services.Interfaces;

using Microsoft.AspNetCore.Http;

namespace Altinn.Platform.Events.Services
{
    /// <summary>
    /// Represents an implementation of <see cref="IClaimsPrincipalProvider"/> using the HttpContext to obtain
    /// the current claims principal needed for the application to make calls to other services.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ClaimsPrincipalProvider : IClaimsPrincipalProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClaimsPrincipalProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor</param>
        public ClaimsPrincipalProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public ClaimsPrincipal GetUser()
        {
            return _httpContextAccessor.HttpContext.User;
        }
    }
}
