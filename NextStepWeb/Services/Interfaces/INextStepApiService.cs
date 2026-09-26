using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NextStepWeb.Models.Api;

namespace NextStepWeb.Services.Interfaces
{
    public interface INextStepApiService
    {
        Task<NextStepApiResponse> AnalyzeSituationAsync(
            string text,
            string? situationId = null,
            Dictionary<string, string>? answers = null,
            CancellationToken cancellationToken = default);

        Task<NextStepApiResponse?> GetExistingAnalysisAsync(
            string situationId,
            CancellationToken cancellationToken = default);
    }
}
