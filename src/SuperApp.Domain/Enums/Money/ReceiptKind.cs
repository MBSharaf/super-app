namespace SuperApp.Domain.Enums;

public enum ReceiptKind : byte
{
    Payment = 0,
    Refund = 1,
    PayoutAdvice = 2,
    TopUp = 3,
    Voucher = 4,
    Statement = 5,
    TaxInvoice = 6,
}
