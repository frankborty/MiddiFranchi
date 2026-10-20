namespace MiddiFranchi.Domain.Financial;

public enum InvestmentOperationKind
{
    Buy,
    Sell,
    Dividend,
    Coupon,
    StandaloneFee,
    TransferIn,
    TransferOut,
    QuantityEvent,
}
