namespace VatVerifier.Api.Contracts;

public sealed record EvaluateInvoiceLineRequest(
    string InvoiceLineId,
    string Description,
    string SupplierName,
    string? SupplierIndustry,
    decimal InvoiceVatRate,
    string? GtuCode = null,
    string? UnitOfMeasure = null,
    bool? ReverseChargeApplied = null,
    bool? SplitPaymentRequired = null);
