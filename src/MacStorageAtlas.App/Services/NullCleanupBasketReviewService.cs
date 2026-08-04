using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public sealed class NullCleanupBasketReviewService : ICleanupBasketReviewService
{
    public Task<bool> ConfirmCleanupAsync(CleanupBasketReview review) =>
        Task.FromResult(false);
}
