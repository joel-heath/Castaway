using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Castaway.Audio;

class AudioPlaybackEngine : IDisposable
{
    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider mixer;
    private LoopStream? loopingMusic;
    private ISampleProvider? loopingMixerInput;

    public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
    {
        outputDevice = new WaveOutEvent();
        mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
        mixer.ReadFully = true;
        outputDevice.Init(mixer);
        outputDevice.Play();
    }

    private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
    {
        if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
        {
            return input;
        }
        if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
        {
            return new MonoToStereoSampleProvider(input);
        }
        throw new NotImplementedException("Not yet implemented this channel count conversion");
    }

    public void PlaySound(CachedSound sound, bool wait = false)
    {
        
        var input = AddMixerInput(new CachedSoundSampleProvider(sound));
        if (wait)
        {
            while (mixer.MixerInputs.Contains(input))
            {
                Thread.Sleep(500);
            }
                
        }
    }

    private ISampleProvider AddMixerInput(ISampleProvider input)
    {
        var mixerInput = ConvertToRightChannelCount(input);
        mixer.AddMixerInput(mixerInput);
        return mixerInput;
    }

    public void Dispose()
    {
        outputDevice.Dispose();
    }

    public void StopLoopingMusic()
    {
        if (loopingMixerInput != null)
        {
            mixer.RemoveMixerInput(loopingMixerInput);
            loopingMixerInput = null;
            loopingMusic?.Dispose();
            loopingMusic = null;
        }
    }

    public void PauseLoopingMusic()
    {
        if (loopingMixerInput != null)
        {
            mixer.RemoveMixerInput(loopingMixerInput);
        }
    }

    public void ResumeLoopingMusic()
    {
        if (loopingMixerInput != null)
        {
            mixer.AddMixerInput(loopingMixerInput);
        }
    }

    public void PlayLoopingMusic(string audioLocation)
    {
        StopLoopingMusic();
        
        this.loopingMusic = new LoopStream(audioLocation);
        
        this.loopingMixerInput = AddMixerInput(loopingMusic.ToSampleProvider());
    }

    public void StopAllSounds()
    {
        StopLoopingMusic();
        mixer.RemoveAllMixerInputs();
    }

    public static readonly AudioPlaybackEngine Instance = new AudioPlaybackEngine(44100, 2);
}
