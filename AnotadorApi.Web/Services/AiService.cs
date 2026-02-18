using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;
using AnotadorApi.Web.Models;
using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using System.Text.Json;

namespace AnotadorApi.Web.Services;

public class AiService
{
    private readonly AppSettings _settings;
    private readonly ILogger<AiService> _logger;

    public AiService(IOptions<AppSettings> settings, ILogger<AiService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ProcessingResult> ProcessTranscriptAsync(string transcript, string language)
    {
        _logger.LogInformation("Processing transcript with Ollama model {Model}", _settings.OllamaModel);

        var ollama = new OllamaApiClient(new Uri(_settings.OllamaUrl));

        var prompt = $$"""
            You are a meeting assistant. Analyze this meeting transcript and extract structured information.

            IMPORTANT: Respond ONLY with valid JSON, no markdown, no code blocks, no other text.

            Transcript language: {{language}}

            Transcript:
            {{transcript}}

            Return this exact JSON structure:
            {
              "title": "short descriptive meeting title in the transcript language",
              "summary": "2-3 paragraph summary in the transcript language",
              "action_items": [
                {"description": "action description", "assignee": "person name or null", "due_date": "YYYY-MM-DD or null"}
              ],
              "reminders": [
                {"description": "reminder description", "remind_at": "ISO 8601 datetime or null"}
              ]
            }
            """;

        var responseBuilder = new StringBuilder();

        await foreach (var stream in ollama.GenerateAsync(new GenerateRequest
        {
            Model = _settings.OllamaModel,
            Prompt = prompt,
            Stream = true
        }))
        {
            if (stream != null)
                responseBuilder.Append(stream.Response);
        }

        var responseText = responseBuilder.ToString().Trim();

        // Try to extract JSON if wrapped in markdown code blocks
        if (responseText.Contains("```"))
        {
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                responseText = responseText[jsonStart..(jsonEnd + 1)];
            }
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<LlmResponse>(responseText, options);

            return new ProcessingResult
            {
                Title = parsed?.Title ?? "Untitled Meeting",
                Summary = parsed?.Summary ?? "",
                ActionItems = parsed?.ActionItems?.Select(a => new ActionItemResult
                {
                    Description = a.Description ?? "",
                    Assignee = a.Assignee,
                    DueDate = DateTime.TryParse(a.DueDate, out var d) ? d : null
                }).ToList() ?? new(),
                Reminders = parsed?.Reminders?.Select(r => new ReminderResult
                {
                    Description = r.Description ?? "",
                    RemindAt = DateTime.TryParse(r.RemindAt, out var d) ? d : DateTime.UtcNow.AddDays(1)
                }).ToList() ?? new()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as JSON. Raw: {Response}", responseText);
            return new ProcessingResult
            {
                Title = "Meeting",
                Summary = responseText,
                ActionItems = new(),
                Reminders = new()
            };
        }
    }
}

// Internal DTOs for LLM response parsing
file class LlmResponse
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public List<LlmActionItem>? ActionItems { get; set; }
    public List<LlmReminder>? Reminders { get; set; }
}

file class LlmActionItem
{
    public string? Description { get; set; }
    public string? Assignee { get; set; }
    public string? DueDate { get; set; }
}

file class LlmReminder
{
    public string? Description { get; set; }
    public string? RemindAt { get; set; }
}
