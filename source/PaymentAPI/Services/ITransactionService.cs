using PaymentAPI.DTOs;

namespace PaymentAPI.Services;

public interface ITransactionService
{
    Task<TransactionHistoryResultDto> GetHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
}
