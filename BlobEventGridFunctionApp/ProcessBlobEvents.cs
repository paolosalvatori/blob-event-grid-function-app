#region Copyright
//=======================================================================================
// This sample is supplemental to the technical guidance published on the community
// blog at https://github.com/paolosalvatori. 
// 
// Author: Paolo Salvatori
//=======================================================================================
// Copyright © 2019 Microsoft Corporation. All rights reserved.
// 
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER 
// EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF 
// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE. YOU BEAR THE RISK OF USING IT.
//=======================================================================================
#endregion

#region Local Endpoint
// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName=ProcessBlobEvents
#endregion

#region Using Directives
using System;
using System.Threading.Tasks;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.ServiceBus;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
#endregion

namespace BlobEventGridFunctionApp
{
    public static class ProcessBlobEvents
    {
        #region Private Constants
        private const string BlobCreatedEvent = "Microsoft.Storage.BlobCreated";
        private const string BlobDeletedEvent = "Microsoft.Storage.BlobDeleted";
        #endregion

        #region Private Static Fields
        private static readonly string key = TelemetryConfiguration.Active.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", EnvironmentVariableTarget.Process);
        private static readonly TelemetryClient telemetry = new TelemetryClient() { InstrumentationKey = key };
        #endregion

        #region Azure Functions
        [FunctionName("ProcessBlobEvents")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent,
                                [ServiceBus("%QueueName%", Connection = "ServiceBusConnectionString", EntityType = EntityType.Queue)] IAsyncCollector<Message> asyncCollector,
                                ExecutionContext context,
                                ILogger log)
        {
            try
            {
                if (eventGridEvent == null && string.IsNullOrWhiteSpace(eventGridEvent.EventType))
                {
                    throw new ArgumentNullException("Null or Invalid Event Grid Event");
                }

                log.LogInformation($@"New Event Grid Event:
    - Id=[{eventGridEvent.Id}]
    - EventType=[{eventGridEvent.EventType}]
    - EventTime=[{eventGridEvent.EventTime}]
    - Subject=[{eventGridEvent.Subject}]
    - Topic=[{eventGridEvent.Topic}]");

                if (eventGridEvent.Data is JObject jObject)
                {
                    // Create message
                    var message = new Message(Encoding.UTF8.GetBytes(jObject.ToString()))
                    {
                        MessageId = eventGridEvent.Id
                    };

                    switch (eventGridEvent.EventType)
                    {
                        case BlobCreatedEvent:
                            {
                                var blobCreatedEvent = jObject.ToObject<StorageBlobCreatedEventData>();
                                var storageDiagnostics = JObject.Parse(blobCreatedEvent.StorageDiagnostics.ToString()).ToString(Newtonsoft.Json.Formatting.None);

                                log.LogInformation($@"Received {BlobCreatedEvent} Event: 
    - Api=[{blobCreatedEvent.Api}]
    - BlobType=[{blobCreatedEvent.BlobType}]
    - ClientRequestId=[{blobCreatedEvent.ClientRequestId}]
    - ContentLength=[{blobCreatedEvent.ContentLength}]
    - ContentType=[{blobCreatedEvent.ContentType}]
    - ETag=[{blobCreatedEvent.ETag}]
    - RequestId=[{blobCreatedEvent.RequestId}]
    - Sequencer=[{blobCreatedEvent.Sequencer}]
    - StorageDiagnostics=[{storageDiagnostics}]
    - Url=[{blobCreatedEvent.Url}]
");

                                // Set message label
                                message.Label = "BlobCreatedEvent";

                                // Add custom properties
                                message.UserProperties.Add("id", eventGridEvent.Id);
                                message.UserProperties.Add("topic", eventGridEvent.Topic);
                                message.UserProperties.Add("eventType", eventGridEvent.EventType);
                                message.UserProperties.Add("eventTime", eventGridEvent.EventTime);
                                message.UserProperties.Add("subject", eventGridEvent.Subject);
                                message.UserProperties.Add("api", blobCreatedEvent.Api);
                                message.UserProperties.Add("blobType", blobCreatedEvent.BlobType);
                                message.UserProperties.Add("clientRequestId", blobCreatedEvent.ClientRequestId);
                                message.UserProperties.Add("contentLength", blobCreatedEvent.ContentLength);
                                message.UserProperties.Add("contentType", blobCreatedEvent.ContentType);
                                message.UserProperties.Add("eTag", blobCreatedEvent.ETag);
                                message.UserProperties.Add("requestId", blobCreatedEvent.RequestId);
                                message.UserProperties.Add("sequencer", blobCreatedEvent.Sequencer);
                                message.UserProperties.Add("storageDiagnostics", storageDiagnostics);
                                message.UserProperties.Add("url", blobCreatedEvent.Url);

                                // Add message to AsyncCollector
                                await asyncCollector.AddAsync(message);

                                // Telemetry
                                telemetry.Context.Operation.Id = context.InvocationId.ToString();
                                telemetry.Context.Operation.Name = "BlobCreatedEvent";
                                telemetry.TrackEvent($"[{blobCreatedEvent.Url}] blob created");
                                var properties = new Dictionary<string, string>
                                {
                                    { "BlobType", blobCreatedEvent.BlobType },
                                    { "ContentType ", blobCreatedEvent.ContentType }
                                };
                                telemetry.TrackMetric("ProcessBlobEvents Created", 1, properties);
                            }
                            break;

                        case BlobDeletedEvent:
                            {
                                var blobDeletedEvent = jObject.ToObject<StorageBlobDeletedEventData>();
                                var storageDiagnostics = JObject.Parse(blobDeletedEvent.StorageDiagnostics.ToString()).ToString(Newtonsoft.Json.Formatting.None);

                                log.LogInformation($@"Received {BlobDeletedEvent} Event: 
    - Api=[{blobDeletedEvent.Api}]
    - BlobType=[{blobDeletedEvent.BlobType}]
    - ClientRequestId=[{blobDeletedEvent.ClientRequestId}]
    - ContentType=[{blobDeletedEvent.ContentType}]
    - RequestId=[{blobDeletedEvent.RequestId}]
    - Sequencer=[{blobDeletedEvent.Sequencer}]
    - StorageDiagnostics=[{storageDiagnostics}]
    - Url=[{blobDeletedEvent.Url}]
");

                                // Set message label
                                message.Label = "BlobDeletedEvent";

                                // Add custom properties
                                message.UserProperties.Add("id", eventGridEvent.Id);
                                message.UserProperties.Add("topic", eventGridEvent.Topic);
                                message.UserProperties.Add("eventType", eventGridEvent.EventType);
                                message.UserProperties.Add("eventTime", eventGridEvent.EventTime);
                                message.UserProperties.Add("subject", eventGridEvent.Subject);
                                message.UserProperties.Add("api", blobDeletedEvent.Api);
                                message.UserProperties.Add("blobType", blobDeletedEvent.BlobType);
                                message.UserProperties.Add("clientRequestId", blobDeletedEvent.ClientRequestId);
                                message.UserProperties.Add("contentType", blobDeletedEvent.ContentType);
                                message.UserProperties.Add("requestId", blobDeletedEvent.RequestId);
                                message.UserProperties.Add("sequencer", blobDeletedEvent.Sequencer);
                                message.UserProperties.Add("storageDiagnostics", storageDiagnostics);
                                message.UserProperties.Add("url", blobDeletedEvent.Url);

                                // Add message to AsyncCollector
                                await asyncCollector.AddAsync(message);

                                // Telemetry
                                telemetry.Context.Operation.Id = context.InvocationId.ToString();
                                telemetry.Context.Operation.Name = "BlobDeletedEvent";
                                telemetry.TrackEvent($"[{blobDeletedEvent.Url}] blob deleted");
                                var properties = new Dictionary<string, string>
                                {
                                    { "BlobType", blobDeletedEvent.BlobType },
                                    { "ContentType ", blobDeletedEvent.ContentType }
                                };
                                telemetry.TrackMetric("ProcessBlobEvents Deleted", 1, properties);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message);
                throw;
            }
        } 
        #endregion
    }
}
