using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Services.Chatbot
{
    /// <summary>
    /// Implementation of the Customer Support Chatbot Service
    /// Acts as an HTTP client proxy to the Flask-based chatbot API
    /// </summary>
    public class ChatbotService : IChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatbotService> _logger;
        private readonly string _flaskApiBaseUrl;
        private const int TimeoutSeconds = 120; // Allow extra time for LLM processing

        public ChatbotService(HttpClient httpClient, ILogger<ChatbotService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read Flask API URL from configuration, default to localhost:5005
            _flaskApiBaseUrl = configuration["Chatbot:FlaskApiUrl"] ?? "http://localhost:5005";
            _httpClient.BaseAddress = new Uri(_flaskApiBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            _logger.LogInformation($"🤖 ChatbotService initialized with Flask API: {_flaskApiBaseUrl}");
        }

        /// <summary>
        /// Ask the chatbot a question
        /// </summary>
        public async Task<ChatbotResponse> AskAsync(string question)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(question))
                {
                    _logger.LogWarning("Empty question received");
                    return new ChatbotResponse
                    {
                        Success = false,
                        Error = "Question cannot be empty"
                    };
                }

                _logger.LogInformation($"📥 Sending question to chatbot: {question}");

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
                    _logger.LogError($"❌ Flask API error: {response.StatusCode} - {errorContent}");

                    return new ChatbotResponse
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

                var chatbotResponse = JsonSerializer.Deserialize<ChatbotResponse>(responseContent, jsonOptions);

                if (chatbotResponse != null && chatbotResponse.Success)
                {
                    _logger.LogInformation($"✅ Received answer from chatbot (sources: {chatbotResponse.Sources?.Count ?? 0})");
                }

                return chatbotResponse ?? new ChatbotResponse 
                { 
                    Success = false, 
                    Error = "Failed to parse response" 
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"❌ Network error communicating with Flask API: {ex.Message}");
                return new ChatbotResponse
                {
                    Success = false,
                    Error = $"Failed to connect to chatbot service: {ex.Message}",
                    Answer = null
                };
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"❌ Request timeout: {ex.Message}");
                return new ChatbotResponse
                {
                    Success = false,
                    Error = "Chatbot service is taking too long to respond",
                    Answer = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error: {ex.Message}");
                return new ChatbotResponse
                {
                    Success = false,
                    Error = $"An error occurred: {ex.Message}",
                    Answer = null
                };
            }
        }

        /// <summary>
        /// Check the health of the chatbot service
        /// </summary>
        public async Task<HealthCheckResponse> GetHealthAsync()
        {
            try
            {
                _logger.LogInformation("🏥 Checking chatbot health...");

                var response = await _httpClient.GetAsync("/health");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ Health check failed: {response.StatusCode}");
                    return new HealthCheckResponse
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

                var healthResponse = JsonSerializer.Deserialize<HealthCheckResponse>(responseContent, jsonOptions);

                if (healthResponse != null)
                {
                    _logger.LogInformation($"✅ Chatbot health: {healthResponse.Status}, Ready: {healthResponse.ChatbotReady}");
                }

                return healthResponse ?? new HealthCheckResponse 
                { 
                    Status = "unknown", 
                    Timestamp = DateTime.UtcNow 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error checking health: {ex.Message}");
                return new HealthCheckResponse
                {
                    Status = "error",
                    Timestamp = DateTime.UtcNow,
                    ChatbotReady = false
                };
            }
        }

        /// <summary>
        /// Get the chatbot configuration
        /// </summary>
        public async Task<ChatbotConfigResponse> GetConfigAsync()
        {
            try
            {
                _logger.LogInformation("📋 Fetching chatbot configuration...");

                var response = await _httpClient.GetAsync("/config");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ Failed to get config: {response.StatusCode}");
                    return new ChatbotConfigResponse();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonOptions = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };

                var configResponse = JsonSerializer.Deserialize<ChatbotConfigResponse>(responseContent, jsonOptions);

                _logger.LogInformation($"✅ Configuration retrieved: Model={configResponse?.LlmModel}, EmbeddingModel={configResponse?.EmbeddingModel}");

                return configResponse ?? new ChatbotConfigResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error fetching config: {ex.Message}");
                return new ChatbotConfigResponse();
            }
        }
    }
}
