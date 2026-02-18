using System.Diagnostics;
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
            await UpdateProgress(supabase, meetingId, "processing", 0);

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

            // 4. Convert audio to WAV (Whisper requires 16-bit PCM WAV)
            await UpdateProgress(supabase, meetingId, "processing", 10);
            _logger.LogInformation("Converting audio to WAV for meeting: {MeetingId}", meetingId);
            var wavBytes = await ConvertToWavAsync(audioBytes);

            using var audioStream = new MemoryStream(wavBytes);

            // 5. Transcribe with Whisper
            await UpdateProgress(supabase, meetingId, "processing", 20);
            _logger.LogInformation("Transcribing meeting: {MeetingId}", meetingId);
            var transcription = await _transcription.TranscribeAsync(
                audioStream, meetingResponse.Language ?? "auto");

            // 6. Process with Ollama
            await UpdateProgress(supabase, meetingId, "processing", 70);
            _logger.LogInformation("Processing transcript with AI: {MeetingId}", meetingId);
            var result = await _ai.ProcessTranscriptAsync(
                transcription.FullText, transcription.Language);

            // 7. Update meeting with results
            await UpdateProgress(supabase, meetingId, "processing", 90);
            await supabase.From<MeetingRow>()
                .Where(m => m.Id == meetingId)
                .Set(m => m.RefinedTranscript!, transcription.FullText)
                .Set(m => m.Summary!, result.Summary)
                .Set(m => m.Title!, result.Title)
                .Set(m => m.Status!, "completed")
                .Set(m => m.Progress!, 100)
                .Update();

            // 8. Insert action items
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

            // 9. Insert reminders
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

    private async Task<byte[]> ConvertToWavAsync(byte[] inputAudio)
    {
        var tempInput = Path.GetTempFileName();
        var tempOutput = Path.ChangeExtension(Path.GetTempFileName(), ".wav");

        try
        {
            await File.WriteAllBytesAsync(tempInput, inputAudio);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{tempInput}\" -ar 16000 -ac 1 -sample_fmt s16 -y \"{tempOutput}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("ffmpeg conversion failed: {Error}", stderr);
                throw new Exception($"ffmpeg failed with exit code {process.ExitCode}: {stderr}");
            }

            _logger.LogInformation("Audio converted to WAV successfully");
            return await File.ReadAllBytesAsync(tempOutput);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }

    private static async Task UpdateMeetingStatus(Supabase.Client supabase, Guid meetingId, string status)
    {
        await supabase.From<MeetingRow>()
            .Where(m => m.Id == meetingId)
            .Set(m => m.Status!, status)
            .Update();
    }

    private static async Task UpdateProgress(Supabase.Client supabase, Guid meetingId, string status, int progress)
    {
        await supabase.From<MeetingRow>()
            .Where(m => m.Id == meetingId)
            .Set(m => m.Status!, status)
            .Set(m => m.Progress!, progress)
            .Update();
    }
}
