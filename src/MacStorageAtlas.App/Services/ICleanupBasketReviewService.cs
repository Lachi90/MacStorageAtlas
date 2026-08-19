using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public interface ICleanupBasketReviewService
{
    Task<bool> ConfirmCleanupAsync(CleanupBasketReview review);
}
