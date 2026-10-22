using System.Data;

namespace DiaCompanion.Api.Repositories;

/// <summary>
/// Đơn vị công việc của một request. EfRepository triển khai luôn interface này,
/// vì vậy DI hiện tại vẫn chỉ cần một scoped IRepository/EfRepository.
/// </summary>
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
    Task<bool> TryCommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Lưu; trả false (và hủy các thay đổi đang chờ) khi vi phạm chỉ mục/ràng buộc DUY NHẤT
    /// (SQL Server 2601, 2627). Lỗi khác vẫn ném ra.</summary>
    Task<bool> TryCommitUniqueAsync(CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
