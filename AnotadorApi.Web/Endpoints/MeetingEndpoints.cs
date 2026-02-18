using AnotadorApi.Web.Services;

namespace AnotadorApi.Web.Endpoints;

public static class MeetingEndpoints
{
    public static void MapMeetingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/meetings");

        group.MapPost("/{meetingId:guid}/process", async (
            Guid meetingId,
            MeetingProcessorService processor) =>
        {
            _ = Task.Run(() => processor.ProcessMeetingAsync(meetingId));
            return Results.Accepted();
        });
    }
}
