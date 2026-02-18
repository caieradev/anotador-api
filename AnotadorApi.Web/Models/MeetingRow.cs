using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AnotadorApi.Web.Models;

[Table("meetings")]
public class MeetingRow : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("language")]
    public string? Language { get; set; }

    [Column("audio_url")]
    public string? AudioUrl { get; set; }

    [Column("raw_transcript")]
    public string? RawTranscript { get; set; }

    [Column("refined_transcript")]
    public string? RefinedTranscript { get; set; }

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("progress")]
    public int? Progress { get; set; }
}

[Table("action_items")]
public class ActionItemRow : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("meeting_id")]
    public Guid MeetingId { get; set; }

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("assignee")]
    public string? Assignee { get; set; }

    [Column("due_date")]
    public DateOnly? DueDate { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";
}

[Table("reminders")]
public class ReminderRow : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("meeting_id")]
    public Guid MeetingId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("remind_at")]
    public DateTime RemindAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";
}
