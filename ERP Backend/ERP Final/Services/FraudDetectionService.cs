using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERPSystem.Services.Chatbot;
using ERPSystem.Services.HRChatbot;
using ERPSystem.Services.ResumeMatching;


/// <summary>
/// Represents the fraud detection service domain model.
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FraudDetectionService> _logger;
    private readonly IConfiguration _configuration;

    public FraudDetectionService(HttpClient httpClient, ILogger<FraudDetectionService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Calls the Flask fraud detection API to predict if a transaction is fraudulent.
    /// </summary>
    public async Task<Result<FraudDetectionResponse>> PredictFraudAsync(List<double> features)
    {
        try
        {
            // Validate input
            if (features == null || features.Count != 30)
            {
                _logger.LogWarning("Invalid feature count: expected 30, got {FeatureCount}", features?.Count ?? 0);
                return Result<FraudDetectionResponse>.Failure("Invalid feature count. Expected exactly 30 features (V1-V28, Amount, Time).");
            }

            // Create request
            var request = new FraudDetectionRequest { Features = features };
            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Call Flask API
            var response = await _httpClient.PostAsync("/predict", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Fraud detection API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                return Result<FraudDetectionResponse>.Failure($"Fraud detection API error: {response.StatusCode}");
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync();
            var fraudResponse = JsonSerializer.Deserialize<FraudDetectionResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (fraudResponse == null)
            {
                _logger.LogError("Failed to deserialize fraud detection response");
                return Result<FraudDetectionResponse>.Failure("Failed to parse fraud detection response.");
            }

            _logger.LogInformation("Fraud detection completed - Label: {Label}, Probability: {Probability}%, Confidence: {Confidence}%",
                fraudResponse.Label, fraudResponse.FraudProbability, fraudResponse.Confidence);

            return Result<FraudDetectionResponse>.Success(fraudResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling fraud detection API");
            return Result<FraudDetectionResponse>.Failure($"Failed to communicate with fraud detection service: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in fraud detection");
            return Result<FraudDetectionResponse>.Failure($"Unexpected error during fraud detection: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the Flask fraud detection API is accessible.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Fraud detection API health check passed");
                return true;
            }

            _logger.LogWarning("Fraud detection API health check failed with status {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fraud detection API health check failed");
            return false;
        }
    }
}

// HR ATTRITION SERVICE IMPLEMENTATION

/// <summary>
/// Service for HR Attrition prediction integration with Flask API
/// </summary>
public class HRAttritionService : IHRAttritionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HRAttritionService> _logger;
    private readonly IConfiguration _configuration;

    public HRAttritionService(HttpClient httpClient, ILogger<HRAttritionService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Calls the Flask HR Attrition API to predict employee attrition risk.
    /// </summary>
    public async Task<Result<HRAttritionResponse>> PredictAttritionAsync(List<double> features)
    {
        try
        {
            // Validate input
            if (features == null || features.Count != 30)
            {
                _logger.LogWarning("Invalid feature count: expected 30, got {FeatureCount}", features?.Count ?? 0);
                return Result<HRAttritionResponse>.Failure("Invalid feature count. Expected exactly 30 employee features.");
            }

            // Create request
            var request = new HRAttritionRequest { Features = features };
            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Call Flask API
            var response = await _httpClient.PostAsync("/predict", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("HR Attrition API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                return Result<HRAttritionResponse>.Failure($"HR Attrition API error: {response.StatusCode}");
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync();
            var attritionResponse = JsonSerializer.Deserialize<HRAttritionResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (attritionResponse == null)
            {
                _logger.LogError("Failed to deserialize HR Attrition response");
                return Result<HRAttritionResponse>.Failure("Failed to parse HR Attrition response.");
            }

            _logger.LogInformation("HR Attrition prediction completed - Prediction: {Prediction}, Risk Level: {RiskLevel}, Confidence: {Confidence}%, Attrition Probability: {AttritionProbability}%",
                attritionResponse.Prediction, attritionResponse.RiskLevel, attritionResponse.Confidence, attritionResponse.AttritionProbability);

            return Result<HRAttritionResponse>.Success(attritionResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling HR Attrition API");
            return Result<HRAttritionResponse>.Failure($"Failed to communicate with HR Attrition service: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in HR Attrition prediction");
            return Result<HRAttritionResponse>.Failure($"Unexpected error during HR Attrition prediction: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the Flask HR Attrition API is accessible.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("HR Attrition API health check passed");
                return true;
            }

            _logger.LogWarning("HR Attrition API health check failed with status {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HR Attrition API health check failed");
            return false;
        }
    }
}

