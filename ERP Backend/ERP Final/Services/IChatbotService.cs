using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERPSystem.Services.Chatbot
{
    /// <summary>
    /// Interface for Customer Support Chatbot Service
    /// Provides operations for interacting with the Flask-based chatbot API
    /// </summary>
    public interface IChatbotService
    {
        /// <summary>
        /// Ask the chatbot a question and get an answer
        /// </summary>
        /// <param name="question">The question to ask</param>
        /// <returns>ChatbotResponse containing the answer and metadata</returns>
        Task<ChatbotResponse> AskAsync(string question);

        /// <summary>
        /// Check if the chatbot service is healthy and ready
        /// </summary>
        /// <returns>HealthCheckResponse with status information</returns>
        Task<HealthCheckResponse> GetHealthAsync();

        /// <summary>
        /// Get the current configuration of the chatbot
        /// </summary>
        /// <returns>ChatbotConfigResponse with configuration details</returns>
        Task<ChatbotConfigResponse> GetConfigAsync();
    }

    /// <summary>
    /// Response model for chatbot queries
    /// </summary>
    public class ChatbotResponse
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
    public class HealthCheckResponse
    {
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public bool ChatbotReady { get; set; }
    }

    /// <summary>
    /// Response model for configuration
    /// </summary>
    public class ChatbotConfigResponse
    {
        public string EmbeddingModel { get; set; }
        public int ChunkSize { get; set; }
        public int RetrievalK { get; set; }
        public string LlmModel { get; set; }
        public double Temperature { get; set; }
        public string PdfFile { get; set; }
    }
}
