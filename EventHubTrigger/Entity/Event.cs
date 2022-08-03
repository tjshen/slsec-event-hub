using System.Collections.Generic;

namespace EventHubTrigger.Entity;

public class Event
{
    public List<string> StandardBundles { get; set; }
    public List<PricingPlan> PricingPlans { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Kind { get; set; }
    public Properties Properties { get; set; }
}