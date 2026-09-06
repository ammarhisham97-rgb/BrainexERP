using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERPSystem.Services.HRChatbot
{
    /// <summary>
    /// Interface for HR Policy Chatbot Service
    /// Provides operations for interacting with the Flask-based HR chatbot API
    /// </summary>
    public interface IHRChatbotService
    {
        /// <summary>
        /// Ask the HR chatbot a question about HR policies
        /// </summary>
        /// <param name="question">The HR policy question to ask</param>
        /// <returns>HRChatbotResponse containing the answer and metadata</returns>
        Task<HRChatbotResponse> AskAsync(string question);

        /// <summary>
        /// Check if the HR chatbot service is healthy and ready
        /// </summary>
        /// <returns>HealthCheckResponse with status information</returns>
        Task<HRChatbotHealthResponse> GetHealthAsync();

        /// <summary>
        /// Get the current configuration of the HR chatbot
        /// </summary>
        /// <returns>HRChatbotConfigResponse with configuration details</returns>
        Task<HRChatbotConfigResponse> GetConfigAsync();
    }

    /// <summary>
    /// Response model for HR chatbot queries
    /// </summary>
    public class HRChatbotResponse
    {
        public bool Success { get; set; }
        public string Answer { get; set; }
        public List<int> Sources { get; set; } = new List<int>();
        public double ProcessingTimeMs { get; set; }
        public int DocumentsRetrieved { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Response model for health checks
    /// </summary>
    public class HRChatbotHealthResponse
    {
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public bool ChatbotReady { get; set; }
    }

    /// <summary>
    /// Response model for configuration
    /// </summary>
    public class HRChatbotConfigResponse
    {
        public string EmbeddingModel { get; set; }
        public int ChunkSize { get; set; }
        public int RetrievalK { get; set; }
        public string LlmModel { get; set; }
        public double Temperature { get; set; }
        public string PdfFile { get; set; }
    }
}
