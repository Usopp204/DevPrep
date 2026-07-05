namespace PaymentAPI.DTOs;

public class TransactionHistoryResultDto
{
    public WalletBalanceDto Balance { get; set; } = new();
    public PagedResult<TransactionDto> Transactions { get; set; } = new();
}
