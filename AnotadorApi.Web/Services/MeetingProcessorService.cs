using Microsoft.Extensions.Options;
using AnotadorApi.Web.Configuration;
using AnotadorApi.Web.Models;

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

        var supabase = new Supabase.Client(
            _settings.SupabaseUrl,
            _settings.SupabaseServiceKey);
        await supabase.InitializeAsync();

        try
        {
            // 1. Update status to processing
            await supabase.From<MeetingRow>()
                .Where(m => m.Id == meetingId)
                .Set(m => m.Status!, "processing")
                .Update();

            // 2. Get meeting data
            var meetingResponse = await supabase.From<MeetingRow>()
                .Where(m => m.Id == meetingId)
                .Single();

            if (meetingResponse == null)
            {
                _logger.LogError("Meeting not found: {MeetingId}", meetingId);
                return;
            }

            // 3. Download audio from storage
            var audioUrl = meetingResponse.AudioUrl;
            if (string.IsNullOrEmpty(audioUrl))
            {
                _logger.LogError("No audio URL for meeting: {MeetingId}", meetingId);
                await UpdateMeetingStatus(supabase, meetingId, "failed");
                return;
            }

            var audioBytes = await supabase.Storage.From("meeting-audio").Download(audioUrl, null);
            if (audioBytes == null || audioBytes.Length == 0)
            {
                _logger.LogError("Failed to download audio for meeting: {MeetingId}", meetingId);
                await UpdateMeetingStatus(supabase, meetingId, "failed");
                return;
            }

            using var audioStream = new MemoryStream(audioBytes);

            // 4. Transcribe with Whisper
            _logger.LogInformation("Transcribing meeting: {MeetingId}", meetingId);
            var transcription = await _transcription.TranscribeAsync(
                audioStream, meetingResponse.Language ?? "auto");

            // 5. Process with Ollama
            _logger.LogInformation("Processing transcript with AI: {MeetingId}", meetingId);
            var result = await _ai.ProcessTranscriptAsync(
                transcription.FullText, transcription.Language);

            // 6. Update meeting with results
            await supabase.From<MeetingRow>()
                .Where(m => m.Id == meetingId)
                .Set(m => m.RefinedTranscript!, transcription.FullText)
                .Set(m => m.Summary!, result.Summary)
                .Set(m => m.Title!, result.Title)
                .Set(m => m.Status!, "completed")
                .Update();

            // 7. Insert action items
            foreach (var item in result.ActionItems)
            {
                await supabase.From<ActionItemRow>()
                    .Insert(new ActionItemRow
                    {
                        MeetingId = meetingId,
                        Description = item.Description,
                        Assignee = item.Assignee,
                        DueDate = item.DueDate.HasValue ? DateOnly.FromDateTime(item.DueDate.Value) : null,
                        Status = "pending"
                    });
            }

            // 8. Insert reminders
            foreach (var reminder in result.Reminders)
            {
                await supabase.From<ReminderRow>()
                    .Insert(new ReminderRow
                    {
                        MeetingId = meetingId,
                        UserId = meetingResponse.UserId,
                        Description = reminder.Description,
                        RemindAt = reminder.RemindAt,
                        Status = "pending"
                    });
            }

            _logger.LogInformation("Meeting processed successfully: {MeetingId}", meetingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process meeting: {MeetingId}", meetingId);
            await UpdateMeetingStatus(supabase, meetingId, "failed");
        }
    }

    private static async Task UpdateMeetingStatus(Supabase.Client supabase, Guid meetingId, string status)
    {
        await supabase.From<MeetingRow>()
            .Where(m => m.Id == meetingId)
            .Set(m => m.Status!, status)
            .Update();
    }
}
