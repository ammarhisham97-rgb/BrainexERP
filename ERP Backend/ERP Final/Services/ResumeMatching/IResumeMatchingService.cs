namespace ERPSystem.Services.ResumeMatching
{
    /// <summary>
    /// Interface for resume matching service that integrates with Flask AI model
    /// </summary>
    public interface IResumeMatchingService
    {
        /// <summary>
        /// Match resumes against a job description using Flask service
        /// </summary>
        /// <param name="jobDescription">Job posting description/requirements</param>
        /// <param name="resumes">List of resumes to match</param>
        /// <returns>Matching results with top 5 candidates</returns>
        Task<ResumeMatchingResponse> MatchResumesAsync(string jobDescription, List<ResumeData> resumes);

        /// <summary>
        /// Match candidates for a specific job posting
        /// </summary>
        /// <param name="jobPostingId">ID of the job posting</param>
        /// <param name="jobDescription">Description of the job</param>
        /// <param name="candidates">List of candidates to evaluate</param>
        /// <returns>Ranked list of candidate matches</returns>
        Task<MatchCandidatesResponse> MatchCandidatesForJobAsync(
            Guid jobPostingId, 
            string jobDescription, 
            List<(Guid Id, string Name, string Text, string Category)> candidates);

        /// <summary>
        /// Check health status of Flask resume matching service
        /// </summary>
        /// <returns>True if service is healthy, false otherwise</returns>
        Task<bool> HealthCheckAsync();
    }
}
