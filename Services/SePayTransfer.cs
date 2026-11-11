namespace OutfitStudio.Services;

public sealed record SePayTransfer(string? AccountNumber, string? Code, string? Content,
    string? TransferType, decimal TransferAmount, string? ReferenceCode);
