using System;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Carrier;

public class WebhookPayloadDto
{
    [Required]
    public string TrackingNumber { get; set; } = string.Empty;
    
    [Required]
    public string NewStatus { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; }
    
    public string CarrierName { get; set; } = string.Empty;
}
