using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;
using Whisper.net;

namespace AnotadorApi.Web.Services;

public class TranscriptionService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ILogger<TranscriptionService> _logger;
    private WhisperProcessor? _processor;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public TranscriptionService(IOptions<AppSettings> settings, ILogger<TranscriptionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private async Task<WhisperProcessor> GetProcessorAsync()
    {
        if (_processor != null) return _processor;

        await _semaphore.WaitAsync();
        try
        {
            if (_processor != null) return _processor;

            _logger.LogInformation("Loading Whisper model from {Path}", _settings.WhisperModelPath);

            var factory = WhisperFactory.FromPath(_settings.WhisperModelPath);
            _processor = factory.CreateBuilder()
                .WithLanguageDetection()
                .Build();

            return _processor;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, string language = "auto")
    {
        _logger.LogInformation("Transcribing audio, language hint: {Language}", language);

        var processor = await GetProcessorAsync();
        var segments = new List<TranscriptionSegment>();

        await foreach (var segment in processor.ProcessAsync(audioStream))
        {
            segments.Add(new TranscriptionSegment
            {
                Start = segment.Start,
                End = segment.End,
                Text = segment.Text
            });
        }

        var fullText = string.Join(" ", segments.Select(s => s.Text.Trim()));
        var detectedLanguage = language == "auto" ? "detected" : language;

        return new TranscriptionResult
        {
            FullText = fullText,
            Segments = segments,
            Language = detectedLanguage
        };
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _semaphore.Dispose();
    }
}

public class TranscriptionResult
{
    public string FullText { get; set; } = "";
    public List<TranscriptionSegment> Segments { get; set; } = new();
    public string Language { get; set; } = "";
}

public class TranscriptionSegment
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Text { get; set; } = "";
}
