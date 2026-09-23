using System;
using System.Collections.Generic;

namespace Application.DTOs.CustomerAdmin;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateCustomerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class CustomerSnapshotDto
{
    public CustomerDto Profile { get; set; } = null!;
    public List<CustomerAddressDto> LastAddresses { get; set; } = new();
    public decimal CurrentWalletBalance { get; set; }
    public List<SarInvoiceDto> LastInvoices { get; set; } = new();
}

public class CustomerAddressDto
{
    public int AddressId { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class SarInvoiceDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CAI { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime IssuedAt { get; set; }
}
