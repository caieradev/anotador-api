using AnotadorApi.Web.Configuration;
using AnotadorApi.Web.Endpoints;
using AnotadorApi.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddScoped<MeetingProcessorService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

app.MapHealthEndpoints();
app.MapMeetingEndpoints();

app.Run();
