using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;
using Whisper.net;

namespace AnotadorApi.Web.Services;

public class TranscriptionService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ILogger<TranscriptionService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public TranscriptionService(IOptions<AppSettings> settings, ILogger<TranscriptionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private WhisperFactory? _factory;

    private WhisperFactory GetFactory()
    {
        if (_factory != null) return _factory;

        _logger.LogInformation("Loading Whisper model from {Path}", _settings.WhisperModelPath);
        _factory = WhisperFactory.FromPath(_settings.WhisperModelPath);
        return _factory;
    }

    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, string language = "auto")
    {
        _logger.LogInformation("Transcribing audio, language hint: {Language}", language);

        await _semaphore.WaitAsync();
        try
        {
        var factory = GetFactory();
        var builder = factory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount);

        if (language != "auto" && language != "detected")
        {
            var langCode = language.Split('_')[0]; // pt_BR -> pt
            _logger.LogInformation("Forcing language: {Lang}", langCode);
            builder.WithLanguage(langCode);
        }
        else
        {
            builder.WithLanguageDetection();
        }

        using var processor = builder.Build();
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

        _logger.LogInformation("Transcription complete: {SegmentCount} segments, {Length} chars", segments.Count, fullText.Length);

        return new TranscriptionResult
        {
            FullText = fullText,
            Segments = segments,
            Language = detectedLanguage
        };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
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
