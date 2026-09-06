using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ERPSystem.Services.HRChatbot
{
    /// <summary>
    /// Implementation of the HR Policy Chatbot Service
    /// Acts as an HTTP client proxy to the Flask-based HR chatbot API
    /// </summary>
    public class HRChatbotService : IHRChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HRChatbotService> _logger;
        private readonly string _flaskApiBaseUrl;
        private const int TimeoutSeconds = 120; // Allow extra time for LLM processing

        public HRChatbotService(HttpClient httpClient, ILogger<HRChatbotService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read Flask API URL from configuration, default to localhost:5004
            _flaskApiBaseUrl = configuration["HRChatbot:FlaskApiUrl"] ?? "http://localhost:5004";
            _httpClient.BaseAddress = new Uri(_flaskApiBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            _logger.LogInformation($"🤖 HRChatbotService initialized with Flask API: {_flaskApiBaseUrl}");
        }

        /// <summary>
        /// Ask the HR chatbot a question about HR policies
        /// </summary>
        public async Task<HRChatbotResponse> AskAsync(string question)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(question))
                {
                    _logger.LogWarning("Empty question received");
                    return new HRChatbotResponse
                    {
                        Success = false,
                        Error = "Question cannot be empty"
                    };
                }

                _logger.LogInformation($"📥 Sending HR question to chatbot: {question}");

                // Prepare request
                var requestBody = new { question = question.Trim() };
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                // Call Flask API
                var response = await _httpClient.PostAsync("/ask", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"❌ Flask HR API error: {response.StatusCode} - {errorContent}");

                    return new HRChatbotResponse
                    {
                        Success = false,
                        Error = $"Flask API returned {response.StatusCode}",
                        Answer = null
                    };
                }

                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };

                var chatbotResponse = JsonSerializer.Deserialize<HRChatbotResponse>(responseContent, jsonOptions);

                if (chatbotResponse != null && chatbotResponse.Success)
                {
                    _logger.LogInformation($"✅ Received HR answer from chatbot (sources: {chatbotResponse.Sources?.Count ?? 0})");
                }

                return chatbotResponse ?? new HRChatbotResponse 
                { 
                    Success = false, 
                    Error = "Failed to parse response" 
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"❌ Network error communicating with HR Flask API: {ex.Message}");
                return new HRChatbotResponse
                {
                    Success = false,
                    Error = $"Failed to connect to HR chatbot service: {ex.Message}",
                    Answer = null
                };
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"⏱️ Request timeout: {ex.Message}");
                return new HRChatbotResponse
                {
                    Success = false,
                    Error = "HR chatbot service is taking too long to respond",
                    Answer = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error: {ex.Message}");
                return new HRChatbotResponse
                {
                    Success = false,
                    Error = $"An error occurred: {ex.Message}",
                    Answer = null
                };
            }
        }

        /// <summary>
        /// Check the health of the HR chatbot service
        /// </summary>
        public async Task<HRChatbotHealthResponse> GetHealthAsync()
        {
            try
            {
                _logger.LogInformation("🏥 Checking HR chatbot health...");

                var response = await _httpClient.GetAsync("/health");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ HR Health check failed: {response.StatusCode}");
                    return new HRChatbotHealthResponse
                    {
                        Status = "unhealthy",
                        Timestamp = DateTime.UtcNow,
                        ChatbotReady = false
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };

                var healthResponse = JsonSerializer.Deserialize<HRChatbotHealthResponse>(responseContent, jsonOptions);

                if (healthResponse != null)
                {
                    _logger.LogInformation($"✅ HR Chatbot health: {healthResponse.Status}, Ready: {healthResponse.ChatbotReady}");
                }

                return healthResponse ?? new HRChatbotHealthResponse 
                { 
                    Status = "unknown", 
                    Timestamp = DateTime.UtcNow 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error checking HR chatbot health: {ex.Message}");
                return new HRChatbotHealthResponse
                {
                    Status = "error",
                    Timestamp = DateTime.UtcNow,
                    ChatbotReady = false
                };
            }
        }

        /// <summary>
        /// Get the HR chatbot configuration
        /// </summary>
        public async Task<HRChatbotConfigResponse> GetConfigAsync()
        {
            try
            {
                _logger.LogInformation("📋 Fetching HR chatbot configuration...");

                var response = await _httpClient.GetAsync("/config");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ Failed to get HR config: {response.StatusCode}");
                    return new HRChatbotConfigResponse();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };

                var configResponse = JsonSerializer.Deserialize<HRChatbotConfigResponse>(responseContent, jsonOptions);

                _logger.LogInformation($"✅ HR Configuration retrieved: Model={configResponse?.LlmModel}, EmbeddingModel={configResponse?.EmbeddingModel}");

                return configResponse ?? new HRChatbotConfigResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error fetching HR chatbot config: {ex.Message}");
                return new HRChatbotConfigResponse();
            }
        }
    }
}
