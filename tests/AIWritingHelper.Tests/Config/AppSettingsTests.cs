using AIWritingHelper.Config;
using Xunit;

namespace AIWritingHelper.Tests.Config;

public class AppSettingsTests
{
    [Fact]
    public void CopyFrom_CopiesAllProperties()
    {
        var source = new AppSettings
        {
            LlmApiEndpoint = "https://custom.api/v1",
            LlmApiKey = "key-123",
            LlmModelName = "custom-model",
            LlmSystemPrompt = "Custom prompt",
            SttApiKey = "stt-key",
            SttModelName = "custom_stt",
            TypoFixHotkey = "Ctrl+Shift+F",
            DictationHotkey = "Ctrl+Shift+D",
            StartWithWindows = true,
            LogLevel = "Debug",
            MicrophoneDeviceName = "My Mic",
            DictationOutputMode = DictationOutputMode.DirectInsertion,
        };

        var target = new AppSettings();
        target.CopyFrom(source);

        Assert.Equal(source.LlmApiEndpoint, target.LlmApiEndpoint);
        Assert.Equal(source.LlmApiKey, target.LlmApiKey);
        Assert.Equal(source.LlmModelName, target.LlmModelName);
        Assert.Equal(source.LlmSystemPrompt, target.LlmSystemPrompt);
        Assert.Equal(source.SttApiKey, target.SttApiKey);
        Assert.Equal(source.SttModelName, target.SttModelName);
        Assert.Equal(source.TypoFixHotkey, target.TypoFixHotkey);
        Assert.Equal(source.DictationHotkey, target.DictationHotkey);
        Assert.Equal(source.StartWithWindows, target.StartWithWindows);
        Assert.Equal(source.LogLevel, target.LogLevel);
        Assert.Equal(source.MicrophoneDeviceName, target.MicrophoneDeviceName);
        Assert.Equal(source.DictationOutputMode, target.DictationOutputMode);
    }

    [Fact]
    public void CopyFrom_TargetIsIndependentFromSource()
    {
        var source = new AppSettings
        {
            LlmApiEndpoint = "https://original.api/v1",
            LlmApiKey = "original-key",
            LlmModelName = "original-model",
        };

        var target = new AppSettings();
        target.CopyFrom(source);

        // Mutate source after copy
        source.LlmApiEndpoint = "https://changed.api/v1";
        source.LlmApiKey = "changed-key";
        source.LlmModelName = "changed-model";

        // Target should retain original values
        Assert.Equal("https://original.api/v1", target.LlmApiEndpoint);
        Assert.Equal("original-key", target.LlmApiKey);
        Assert.Equal("original-model", target.LlmModelName);
    }
}
