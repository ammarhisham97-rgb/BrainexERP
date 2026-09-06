using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP_Final.Tests;

public class HrEnumBindingTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;

    public HrEnumBindingTests(ErpWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("technical")]
    [InlineData("Technical")]
    [InlineData(1)]
    public async Task Post_Hr_Interviews_Accepts_String_And_Numeric_Round(object round)
    {
        var applicationId = await SeedJobApplicationAsync();
        var client = _factory.CreateClient();

        var payload = new
        {
            jobApplicationId = applicationId,
            round,
            scheduledAt = DateTime.UtcNow.AddDays(1),
            interviewerName = "Interviewer"
        };

        var response = await client.PostAsJsonAsync("/api/hr/interviews", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("Open")]
    public async Task Get_Hr_JobPostings_Accepts_Camel_And_Pascal_Case_Status(string status)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/hr/job-postings?status={status}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_Enum_Value_Returns_400_With_Allowed_Values()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/hr/job-postings?status=notAValidStatus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("allowedValues", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Open", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalidValue", content, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> SeedJobApplicationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ERPDbContext>();

        var posting = new JobPosting
        {
            JobCode = $"JOB-{Guid.NewGuid():N}"[..12],
            Title = "Backend Engineer",
            Department = "IT",
            Status = JobPostingStatus.Open,
            OpenDate = DateTime.UtcNow,
            PositionsCount = 1
        };

        db.JobPostings.Add(posting);
        await db.SaveChangesAsync();

        var application = new JobApplication
        {
            JobPostingId = posting.Id,
            FullName = "Applicant One",
            Email = $"applicant-{Guid.NewGuid():N}@example.com",
            Status = ApplicationStatus.Applied,
            AppliedAt = DateTime.UtcNow
        };

        db.JobApplications.Add(application);
        await db.SaveChangesAsync();

        return application.Id;
    }
}
