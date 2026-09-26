using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NextStepWeb.Models.Api;
using NextStepWeb.Services.Interfaces;

namespace NextStepWeb.Services.Implementations
{
    public class NextStepApiService : INextStepApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NextStepApiService> _logger;
        private readonly string _candidateId;

        public NextStepApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<NextStepApiService> _logger)
        {
            this._httpClient = httpClient;
            this._logger = _logger;

            var baseUrl = configuration["NextStepApi:BaseUrl"] ?? "https://nextstepmockapi.onrender.com";
            _candidateId = configuration["NextStepApi:CandidateId"] ?? "candidate@hazhteq.com";

            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-Candidate-Id", _candidateId);

            int timeoutSec = int.TryParse(configuration["NextStepApi:TimeoutSeconds"], out var t) ? t : 25;
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSec);
        }

        public async Task<NextStepApiResponse> AnalyzeSituationAsync(
            string text,
            string? situationId = null,
            Dictionary<string, string>? answers = null,
            CancellationToken cancellationToken = default)
        {
            var requestPayload = new NextStepApiRequest
            {
                Text = text,
                SituationId = situationId,
                Answers = answers
            };

            var jsonBody = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            int maxAttempts = 2;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation("Calling NextStep Mock API POST /v1/situations (Attempt {Attempt}/{Max})", attempt, maxAttempts);

                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "v1/situations")
                    {
                        Content = content
                    };

                    using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests) // 429
                    {
                        _logger.LogWarning("NextStep Mock API returned HTTP 429 Too Many Requests.");
                        return new NextStepApiResponse
                        {
                            Error = "rate_limited",
                            Message = "Analysis is temporarily busy. Your situation is saved and can be analysed again."
                        };
                    }

                    if ((int)response.StatusCode >= 500)
                    {
                        _logger.LogWarning("NextStep Mock API returned server error {StatusCode} on attempt {Attempt}", response.StatusCode, attempt);
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }

                        return new NextStepApiResponse
                        {
                            Error = "server_error",
                            Message = "The analysis service is temporarily unavailable. Your situation has been saved."
                        };
                    }

                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("NextStep Mock API returned error status {StatusCode}: {Body}", response.StatusCode, responseBody);
                        try
                        {
                            var errDto = JsonSerializer.Deserialize<NextStepApiResponse>(responseBody);
                            if (errDto != null && (!string.IsNullOrEmpty(errDto.Error) || !string.IsNullOrEmpty(errDto.Message)))
                            {
                                return errDto;
                            }
                        }
                        catch
                        {
                            // ignore deserialization error for error response
                        }

                        return new NextStepApiResponse
                        {
                            Error = "bad_request",
                            Message = "The analysis request was rejected by the service."
                        };
                    }

                    // Deserialize valid payload
                    var parsed = JsonSerializer.Deserialize<NextStepApiResponse>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return parsed ?? new NextStepApiResponse
                    {
                        Error = "deserialization_failed",
                        Message = "Failed to parse analysis response."
                    };
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Timeout connecting to NextStep Mock API after configured limit.");
                    return new NextStepApiResponse
                    {
                        Error = "timeout",
                        Message = "The analysis is taking longer than expected. Your situation has been saved. Try the analysis again."
                    };
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "HTTP request failed on attempt {Attempt}", attempt);
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    return new NextStepApiResponse
                    {
                        Error = "network_error",
                        Message = "Could not reach the analysis service. Your situation has been saved."
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in NextStepApiService");
                    return new NextStepApiResponse
                    {
                        Error = "unexpected_error",
                        Message = "An unexpected error occurred during analysis. Your situation is safely preserved."
                    };
                }
            }

            return new NextStepApiResponse
            {
                Error = "retries_exhausted",
                Message = "Analysis service is currently busy. Your situation has been saved."
            };
        }

        public async Task<NextStepApiResponse?> GetExistingAnalysisAsync(
            string situationId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(situationId)) return null;

            try
            {
                using var response = await _httpClient.GetAsync($"v1/situations/{situationId}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET /v1/situations/{Id} returned {StatusCode}", situationId, response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<NextStepApiResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching existing analysis for {SituationId}", situationId);
                return null;
            }
        }
    }
}
