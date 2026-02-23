using AIWritingHelper.Audio;
using Xunit;

namespace AIWritingHelper.Tests.Audio;

public class SystemSoundPlayerTests
{
    private readonly SystemSoundPlayer _player = new();

    [Fact]
    public void PlaySuccess_DoesNotThrow()
    {
        _player.PlaySuccess();
    }

    [Fact]
    public void PlayError_DoesNotThrow()
    {
        _player.PlayError();
    }

    [Fact]
    public void PlayRecordingStart_DoesNotThrow()
    {
        _player.PlayRecordingStart();
    }

    [Fact]
    public void PlayRecordingStop_DoesNotThrow()
    {
        _player.PlayRecordingStop();
    }
}
