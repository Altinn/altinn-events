using System;
using System.Text;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.Core;

using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Altinn.Platform.Events.Formatters
{
    // Inspired by: https://github.com/cloudevents/sdk-csharp/blob/main/samples/CloudNative.CloudEvents.AspNetCoreSample/CloudEventJsonInputFormatter.cs
    // FIXME: This doesn't get called for binary CloudEvents without content, or with a different data content type.
    // FIXME: This shouldn't really be tied to JSON. We need to work out how we actually want this to be used.

    /// <summary>
    /// A <see cref="TextInputFormatter"/> that parses HTTP requests into CloudEvents.
    /// </summary>
    public class CloudEventJsonInputFormatter : TextInputFormatter
    {
        private readonly CloudEventFormatter _formatter;

        /// <summary>
        /// Constructs a new instance that uses the given formatter for deserialization.
        /// </summary>
        public CloudEventJsonInputFormatter(CloudEventFormatter formatter)
        {
            _formatter = Validation.CheckNotNull(formatter, nameof(formatter));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/cloudevents+json"));

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        /// <inheritdoc />
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
        {
            Validation.CheckNotNull(context, nameof(context));
            Validation.CheckNotNull(encoding, nameof(encoding));

            var request = context.HttpContext.Request;

            try
            {
                var cloudEvent = await request.ToCloudEventAsync(_formatter);
                return await InputFormatterResult.SuccessAsync(cloudEvent);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <inheritdoc />
        protected override bool CanReadType(Type type)
             => type == typeof(CloudEvent) && base.CanReadType(type);
    }
}