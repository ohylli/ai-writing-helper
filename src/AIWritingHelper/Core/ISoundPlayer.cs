namespace AIWritingHelper.Core;

public interface ISoundPlayer
{
    void PlaySuccess();
    void PlayError();
    void PlayRecordingStart();
    void PlayRecordingStop();
}
