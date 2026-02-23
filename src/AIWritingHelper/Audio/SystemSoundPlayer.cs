using System.Media;
using AIWritingHelper.Core;

namespace AIWritingHelper.Audio;

internal sealed class SystemSoundPlayer : ISoundPlayer
{
    public void PlaySuccess() => SystemSounds.Asterisk.Play();

    public void PlayError() => SystemSounds.Hand.Play();

    public void PlayRecordingStart() => SystemSounds.Beep.Play();

    public void PlayRecordingStop() => SystemSounds.Exclamation.Play();
}
