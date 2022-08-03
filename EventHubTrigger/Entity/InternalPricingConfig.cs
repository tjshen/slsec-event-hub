using System;
using System.Collections.Generic;

namespace EventHubTrigger.Entity;

public class InternalPricingConfig
{
    public Guid SubscriptionId { get; set; }
    
    public Guid TenantId { get; set; }
    
    public string RegistrationState { get; set; }
    
    public List<PricingPlan> PricingPlans { get; set; }
}