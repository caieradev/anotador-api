using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;

namespace AnotadorApi.Web.Services;

public class MeetingProcessorService
{
    private readonly TranscriptionService _transcription;
    private readonly AiService _ai;
    private readonly AppSettings _settings;
    private readonly ILogger<MeetingProcessorService> _logger;

    public MeetingProcessorService(
        TranscriptionService transcription,
        AiService ai,
        IOptions<AppSettings> settings,
        ILogger<MeetingProcessorService> logger)
    {
        _transcription = transcription;
        _ai = ai;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessMeetingAsync(Guid meetingId)
    {
        _logger.LogInformation("Processing meeting: {MeetingId}", meetingId);

        // TODO: 1. Download audio from Supabase Storage
        // TODO: 2. Transcribe with Whisper
        // TODO: 3. Process with Ollama (summary, action items, reminders)
        // TODO: 4. Update meeting in Supabase DB

        await Task.CompletedTask;
    }
}
