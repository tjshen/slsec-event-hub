﻿using Azure.Messaging.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Services.AppAuthentication;

namespace EventHubTrigger
{
    internal static class Function1Helpers
    {
        private static TelemetryClient _telemetryClient;

        private static readonly string URI =
            "https://rp.core.security.dev1.azure.com:8443/internal/enrichedPricingConfigurations?BundleNames=AppServices";
        [FunctionName("Function1")]
        public static async Task Run([EventHubTrigger("glob-rp-core-dev1-prc-eh", Connection = "AzureEventHubConnectionString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            _telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault())
            {
                InstrumentationKey = "29ea7836-3136-4989-938b-2763383acfa6"
            };
            var tokenProvider = new AzureServiceTokenProvider();
            var token = await tokenProvider?.GetAccessTokenAsync("https://rp.core.security.dev1.azure.com/") ??
                        string.Empty;

            foreach (EventData eventData in events)
            {
                try
                {
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {eventData.EventBody}");
                    _telemetryClient.TrackTrace($"C# Event Hub trigger function processed a message: {eventData.EventBody}", SeverityLevel.Information);
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var resp = await client.GetStringAsync(URI);
                    _telemetryClient.TrackTrace($"response string from api is: {resp}");
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            switch (exceptions.Count)
            {
                case > 1:
                    _telemetryClient.TrackTrace($"C# Event Hub trigger function encountered more than one exceptions: {exceptions}");
                    throw new AggregateException(exceptions);
                case 1:
                    _telemetryClient.TrackTrace($"C# Event Hub trigger function encountered one exception: {exceptions}");
                    throw exceptions.Single();
            }
        }
    }
}