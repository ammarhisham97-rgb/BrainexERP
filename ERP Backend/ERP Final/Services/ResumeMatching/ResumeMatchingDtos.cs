using System.Text.Json.Serialization;

namespace ERPSystem.Services.ResumeMatching
{
    /// <summary>
    /// Request DTO for resume matching - sent to Flask service
    /// </summary>
    public class ResumeMatchingRequest
    {
        [JsonPropertyName("jobDescription")]
        public string JobDescription { get; set; } = string.Empty;

        [JsonPropertyName("resumes")]
        public List<ResumeData> Resumes { get; set; } = new();
    }

    /// <summary>
    /// Resume data structure for matching - contains candidate resume details
    /// </summary>
    public class ResumeData
    {
        [JsonPropertyName("ID")]
        public int ID { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Category")]
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response DTO from Flask service - contains matching results
    /// </summary>
    public class ResumeMatchingResponse
    {
        [JsonPropertyName("matchedResumes")]
        public List<MatchedResume> MatchedResumes { get; set; } = new();

        [JsonPropertyName("totalMatches")]
        public int TotalMatches { get; set; }

        [JsonPropertyName("jobDescription")]
        public string JobDescriptionSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Matched resume result with similarity score
    /// </summary>
    public class MatchedResume
    {
        [JsonPropertyName("resumeId")]
        public int ResumeId { get; set; }

        [JsonPropertyName("similarity")]
        public float Similarity { get; set; }

        [JsonPropertyName("domainResume")]
        public string DomainResume { get; set; } = string.Empty;

        [JsonPropertyName("domainDesc")]
        public string DomainDesc { get; set; } = string.Empty;

        [JsonPropertyName("jobId")]
        public int JobId { get; set; }
    }

    /// <summary>
    /// Request DTO for matching candidates against job posting
    /// </summary>
    public class MatchCandidatesRequest
    {
        /// <summary>
        /// Job posting ID to match candidates against
        /// </summary>
        public Guid JobPostingId { get; set; }

        /// <summary>
        /// List of candidate IDs to match
        /// </summary>
        public List<Guid> CandidateIds { get; set; } = new();
    }

    /// <summary>
    /// Response DTO for candidate matching results
    /// </summary>
    public class MatchCandidatesResponse
    {
        /// <summary>
        /// List of matched candidates with scores
        /// </summary>
        public List<CandidateMatchResult> Results { get; set; } = new();

        /// <summary>
        /// Job posting title being matched against
        /// </summary>
        public string JobTitle { get; set; } = string.Empty;

        /// <summary>
        /// Total number of candidates matched
        /// </summary>
        public int TotalMatched { get; set; }

        /// <summary>
        /// Timestamp of matching operation
        /// </summary>
        public DateTime MatchedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Individual candidate match result
    /// </summary>
    public class CandidateMatchResult
    {
        /// <summary>
        /// Candidate ID
        /// </summary>
        public Guid CandidateId { get; set; }

        /// <summary>
        /// Candidate name
        /// </summary>
        public string CandidateName { get; set; } = string.Empty;

        /// <summary>
        /// Similarity score (0-1 range)
        /// </summary>
        public float SimilarityScore { get; set; }

        /// <summary>
        /// Candidate category/domain
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Match rank (1-5 for top 5)
        /// </summary>
        public int Rank { get; set; }
    }
}
