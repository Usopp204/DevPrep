namespace QuestionAPI.Services.Interfaces;

public interface IQuestionIndexEligibilityService
{
    Task<bool> IsReadyAsync(
        Guid categoryId,
        CancellationToken cancellationToken);

    Task SynchronizeCategoryAsync(
        Guid categoryId,
        Guid? changedQuestionId,
        CancellationToken cancellationToken);
}
