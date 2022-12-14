using Azure.Messaging.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EventHubTrigger.Entity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Services.AppAuthentication;
using Newtonsoft.Json;

namespace EventHubTrigger
{
    internal static class Function1Helpers
    {
        private static TelemetryClient _telemetryClient;

        private static readonly string URI =
            "https://rp.core.security.dev1.azure.com:8443/internal/enrichedPricingConfigurations?BundleNames=AppServices";
        private static readonly string COSMOS_URI = Environment.GetEnvironmentVariable("COSMOS_URI")!;
        private static readonly string COSMOS_KEY = Environment.GetEnvironmentVariable("COSMOS_KEY")!;
        
        [FunctionName("Function1")]
        public static async Task Run([EventHubTrigger("glob-rp-core-dev1-prc-eh", Connection = "AzureEventHubConnectionString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            _telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault())
            {
                InstrumentationKey = "29ea7836-3136-4989-938b-2763383acfa6"
            };
            
            CosmosClient cosmosClient = new CosmosClient(COSMOS_URI, COSMOS_KEY);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                id: "testdb"
            );
            
            Container container = await database.CreateContainerIfNotExistsAsync(
                id: "TenantIdMapping",
                partitionKeyPath: "/subscriptionId",
                throughput: 400
            );
            
            var tokenProvider = new AzureServiceTokenProvider();
            var token = await tokenProvider.GetAccessTokenAsync("https://rp.core.security.dev1.azure.com/") ??
                        string.Empty;

            foreach (EventData eventData in events)
            {
                try
                {
                    // Replace these two lines with your processing logic.
                    log.LogInformation("C# Event Hub trigger function processed a message: {EventDataEventBody}", eventData.EventBody);
                    _telemetryClient.TrackTrace($"C# event hub message properties is: {eventData.Properties}");
                    _telemetryClient.TrackTrace($"C# Event Hub trigger function processed a message: {eventData.EventBody}", SeverityLevel.Information);

                    var e = JsonConvert.DeserializeObject<Event>(eventData.EventBody.ToString());
                    _telemetryClient.TrackTrace($"event body is {e}");
                    var id = e.Id.ToString();
                    var subId = id.Split("/")[1];
                    if (!e.StandardBundles.Contains("AppServices"))
                    {
                        _telemetryClient.TrackTrace("Event hub notification doesn't contain app services, need to remove from db!");
                        // Downgrade path
                        var parameterizedQuery = new QueryDefinition(
                            query: "SELECT * FROM items p WHERE p.subscriptionId = @partitionKey"
                        ).WithParameter("@partitionKey", subId);
                        var feed = container.GetItemQueryIterator<Mapping>(
                            queryDefinition: parameterizedQuery);
                        _telemetryClient.TrackTrace($"Feed result is {feed}");
                        while (feed.HasMoreResults)
                        {
                            _telemetryClient.TrackTrace($"Start to process feed result.");
                            FeedResponse<Mapping> cur = await feed.ReadNextAsync();
                            foreach(Mapping ma in cur)
                            {
                                ItemResponse<Mapping> response = await container.DeleteItemAsync<Mapping>(ma.id, new PartitionKey(ma.subscriptionId));
                                _telemetryClient.TrackTrace($"Sucessfully delete item with subscription id: {ma.subscriptionId}");
                            }
                        };
                    }
                    else
                    {
                        // Upgrade path insert
                        _telemetryClient.TrackTrace($"Event hub notification contains the app service, insert the mapping record!");
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        var resp = await client.GetStringAsync(URI);
                        var results = JsonConvert.DeserializeObject<List<InternalPricingConfig>>(resp);
                        foreach (var r in results)
                        {
                            if (r.SubscriptionId.Equals(subId))
                            {
                                _telemetryClient.TrackTrace($"Find the record: {r}");
                                var m = new Mapping(Guid.NewGuid().ToString(), r.SubscriptionId.ToString(), r.TenantId.ToString());
                                var createdItem = await container.UpsertItemAsync(
                                    item: m,
                                    partitionKey: new PartitionKey(r.SubscriptionId.ToString())
                                );
                            }
                            
                        }
                    }
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