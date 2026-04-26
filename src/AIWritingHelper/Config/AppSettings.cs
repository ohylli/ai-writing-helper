namespace AIWritingHelper.Config;

public class AppSettings
{
    public string LlmApiEndpoint { get; set; } = "https://api.cerebras.ai/v1";
    public string LlmApiKey { get; set; } = "";
    public string LlmModelName { get; set; } = "llama3.1-8b";

    public string LlmSystemPrompt { get; set; } =
        "Fix typos and obvious grammar mistakes in the following text. " +
        "Preserve the original formatting, including markdown syntax, line breaks, and whitespace. " +
        "Do not change the meaning, tone, or style. " +
        "Return only the corrected text with no explanation.";

    public string SttApiKey { get; set; } = "";
    public string SttModelName { get; set; } = "scribe_v2";
    public string TypoFixHotkey { get; set; } = "Ctrl+Alt+Space";
    public string DictationHotkey { get; set; } = "Ctrl+Alt+D";
    public bool StartWithWindows { get; set; } = false;
    public string LogLevel { get; set; } = "Information";
    public string MicrophoneDeviceName { get; set; } = "";
    public DictationOutputMode DictationOutputMode { get; set; } = DictationOutputMode.Clipboard;

    public void CopyFrom(AppSettings source)
    {
        LlmApiEndpoint = source.LlmApiEndpoint;
        LlmApiKey = source.LlmApiKey;
        LlmModelName = source.LlmModelName;
        LlmSystemPrompt = source.LlmSystemPrompt;
        SttApiKey = source.SttApiKey;
        SttModelName = source.SttModelName;
        TypoFixHotkey = source.TypoFixHotkey;
        DictationHotkey = source.DictationHotkey;
        StartWithWindows = source.StartWithWindows;
        LogLevel = source.LogLevel;
        MicrophoneDeviceName = source.MicrophoneDeviceName;
        DictationOutputMode = source.DictationOutputMode;
    }
}
