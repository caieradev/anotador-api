using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;

namespace AnotadorApi.Web.Services;

public class TranscriptionService
{
    private readonly AppSettings _settings;
    private readonly ILogger<TranscriptionService> _logger;

    public TranscriptionService(IOptions<AppSettings> settings, ILogger<TranscriptionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<string> TranscribeAsync(Stream audioStream, string language = "auto")
    {
        _logger.LogInformation("Transcribing audio with language: {Language}", language);
        // TODO: Implement Whisper.net transcription
        return Task.FromResult("Transcription placeholder");
    }
}
