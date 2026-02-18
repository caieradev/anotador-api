using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;
using AnotadorApi.Web.Models;

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

    public Task<ProcessingResult> ProcessTranscriptAsync(string transcript, string language)
    {
        _logger.LogInformation("Processing transcript with LLM, language: {Language}", language);
        // TODO: Implement Ollama processing
        return Task.FromResult(new ProcessingResult
        {
            Title = "Meeting",
            Summary = "Summary placeholder"
        });
    }
}
