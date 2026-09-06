using System.Net.Http.Json;
using System.Text.Json;

namespace ERPSystem.Services.ResumeMatching
{
    /// <summary>
    /// Implementation of resume matching service that integrates with Flask AI backend
    /// </summary>
    public class ResumeMatchingService : IResumeMatchingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ResumeMatchingService> _logger;
        private const string HealthEndpoint = "/health";
        private const string PredictEndpoint = "/predict";

        public ResumeMatchingService(HttpClient httpClient, ILogger<ResumeMatchingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Check if Flask resume matching service is healthy
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(HealthEndpoint);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Resume Matching Service health check passed");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Resume Matching Service health check failed with status {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check on Resume Matching Service");
                return false;
            }
        }

        /// <summary>
        /// Match resumes against job description using Flask service
        /// </summary>
        public async Task<ResumeMatchingResponse> MatchResumesAsync(string jobDescription, List<ResumeData> resumes)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(jobDescription))
                {
                    throw new ArgumentException("Job description cannot be empty", nameof(jobDescription));
                }

                if (resumes == null || resumes.Count == 0)
                {
                    throw new ArgumentException("At least one resume is required", nameof(resumes));
                }

                _logger.LogInformation("Sending {ResumeCount} resumes to Flask service for matching", resumes.Count);

                // Create request payload
                var request = new ResumeMatchingRequest
                {
                    JobDescription = jobDescription,
                    Resumes = resumes
                };

                // Call Flask service
                var response = await _httpClient.PostAsJsonAsync(PredictEndpoint, request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Flask service returned error {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Flask service error: {response.StatusCode} - {errorContent}");
                }

                // Parse response
                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                var result = System.Text.Json.JsonSerializer.Deserialize<ResumeMatchingResponse>(jsonContent, options);
                _logger.LogInformation("Successfully matched {MatchCount} resumes", result?.TotalMatches ?? 0);

                return result ?? new ResumeMatchingResponse();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Flask resume matching service");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing Flask service response");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in resume matching");
                throw;
            }
        }

        /// <summary>
        /// Match candidates for a specific job posting
        /// </summary>
        public async Task<MatchCandidatesResponse> MatchCandidatesForJobAsync(
            Guid jobPostingId,
            string jobDescription,
            List<(Guid Id, string Name, string Text, string Category)> candidates)
        {
            try
            {
                // Validate inputs
                if (jobPostingId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid job posting ID", nameof(jobPostingId));
                }

                if (string.IsNullOrWhiteSpace(jobDescription))
                {
                    throw new ArgumentException("Job description cannot be empty", nameof(jobDescription));
                }

                if (candidates == null || candidates.Count == 0)
                {
                    throw new ArgumentException("At least one candidate is required", nameof(candidates));
                }

                _logger.LogInformation("Matching {CandidateCount} candidates for job posting {JobPostingId}", candidates.Count, jobPostingId);

                // Convert candidates to ResumeData format for Flask service
                var resumeDataList = candidates
                    .Select((c, index) => new ResumeData
                    {
                        ID = index + 1,
                        Text = c.Text ?? "",
                        Category = c.Category ?? "General"
                    })
                    .ToList();

                // Call Flask service
                var flaskResponse = await MatchResumesAsync(jobDescription, resumeDataList);

                // Map Flask response to candidates response
                var candidateResults = new List<CandidateMatchResult>();
                var candidateList = candidates.ToList();

                for (int i = 0; i < flaskResponse.MatchedResumes.Count; i++)
                {
                    var matchedResume = flaskResponse.MatchedResumes[i];
                    var candidateIndex = matchedResume.ResumeId - 1;

                    if (candidateIndex < candidateList.Count)
                    {
                        var candidate = candidateList[candidateIndex];
                        candidateResults.Add(new CandidateMatchResult
                        {
                            CandidateId = candidate.Id,
                            CandidateName = candidate.Name,
                            SimilarityScore = matchedResume.Similarity,
                            Category = matchedResume.DomainResume,
                            Rank = i + 1
                        });
                    }
                }

                _logger.LogInformation("Successfully matched {MatchCount} candidates for job {JobPostingId}", candidateResults.Count, jobPostingId);

                return new MatchCandidatesResponse
                {
                    Results = candidateResults,
                    JobTitle = flaskResponse.JobDescriptionSummary,
                    TotalMatched = candidateResults.Count,
                    MatchedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error matching candidates for job posting {JobPostingId}", jobPostingId);
                throw;
            }
        }
    }
}
