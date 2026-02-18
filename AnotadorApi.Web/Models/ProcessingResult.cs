namespace AnotadorApi.Web.Models;

public class ProcessingResult
{
    public string Summary { get; set; } = "";
    public string Title { get; set; } = "";
    public List<ActionItemResult> ActionItems { get; set; } = new();
    public List<ReminderResult> Reminders { get; set; } = new();
}

public class ActionItemResult
{
    public string Description { get; set; } = "";
    public string? Assignee { get; set; }
    public DateTime? DueDate { get; set; }
}

public class ReminderResult
{
    public string Description { get; set; } = "";
    public DateTime RemindAt { get; set; }
}
