using System;
using System.Threading;
using System.Threading.Tasks;
using NextStepWeb.Models.ViewModels;

namespace NextStepWeb.Services.Interfaces
{
    public interface ISituationService
    {
        Task<(SituationResultViewModel? Result, string? ErrorMessage, bool CanRetry)> ProcessNewSituationAsync(
            string situationText,
            string clientRequestId,
            CancellationToken cancellationToken = default);

        Task<(SituationResultViewModel? Result, string? ErrorMessage, bool IsStale)> ProcessSituationUpdateAsync(
            UpdateSituationInputModel input,
            CancellationToken cancellationToken = default);

        Task<SituationResultViewModel?> GetSituationViewModelAsync(
            Guid situationId,
            int? versionNumber = null,
            CancellationToken cancellationToken = default);

        Task<StaleCheckResult> CheckStalenessAsync(
            Guid situationId,
            int clientVersion,
            CancellationToken cancellationToken = default);
    }
}
