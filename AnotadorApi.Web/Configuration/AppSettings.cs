namespace AnotadorApi.Web.Configuration;

public class AppSettings
{
    public string SupabaseUrl { get; set; } = "";
    public string SupabaseServiceKey { get; set; } = "";
    public string WhisperModelPath { get; set; } = "models/ggml-large-v3.bin";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.1";
}
