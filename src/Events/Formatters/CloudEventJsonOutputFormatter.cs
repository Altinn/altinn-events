﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Altinn.Platform.Events.Extensions;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Altinn.Platform.Events.Formatters
{
    /// <summary>
    /// A <see cref="TextOutputFormatter"/> that parses HTTP requests into CloudEvents.
    /// Inspired by: https://github.com/cloudevents/sdk-csharp/blob/main/samples/CloudNative.CloudEvents.AspNetCoreSample/CloudEventJsonOutputFormatter.cs
    /// </summary>
    public class CloudEventJsonOutputFormatter : TextOutputFormatter
    {
        private readonly CloudEventFormatter _formatter;

        /// <summary>
        /// Constructs a new instance that uses the given formatter for deserialization.
        /// </summary>
        public CloudEventJsonOutputFormatter(CloudEventFormatter formatter)
        {            
            _formatter = Validation.CheckNotNull(formatter, nameof(formatter));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/cloudevents+json"));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        /// <inheritdoc />
        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            var response = context.HttpContext.Response;
            if (context.Object is CloudEvent)
            {
                await response.WriteAsync((context.Object as CloudEvent).Serialize(_formatter));
                return;
            }

            if (context.Object is IEnumerable<CloudEvent>)
            {
                var cloudEvents = new List<CloudEvent>(context.Object as IEnumerable<CloudEvent>);

                await response.WriteAsync("[");
                for (int i = 0; i < cloudEvents.Count; i++)
                {
                    await response.WriteAsync(cloudEvents[i].Serialize(_formatter));
                    if (i != cloudEvents.Count - 1)
                    {
                        await response.WriteAsync(", ");
                    }
                }

                await response.WriteAsync("]");
            }
        }

        /// <inheritdoc />
        protected override bool CanWriteType(Type type)
        {
            if (typeof(CloudEvent).IsAssignableFrom(type)
                || typeof(IEnumerable<CloudEvent>).IsAssignableFrom(type))
            {
                return true;
            }

            return false;
        }
    }
}
