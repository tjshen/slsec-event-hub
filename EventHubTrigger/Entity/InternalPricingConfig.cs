using System.Collections.Generic;

namespace EventHubTrigger.Entity;

public class InternalPricingConfig
{
    public string SubScriptionId { get; set; }
    
    public string TenantId { get; set; }
    
    public string RegistrationState { get; set; }
    
    public List<PricingPlan> PricingPlans { get; set; }
}