using System;
using System.IO;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ShuffleDrop
{
    internal sealed class AudioPreview : IDisposable
    {
        MediaFoundationReader reader;
        WaveChannel32 channel;
        WasapiOut output;
        Exception playbackError;
        float volume = 0.65f;
        public bool Opened { get { return output != null; } }
        public bool Paused { get { return output != null && output.PlaybackState == PlaybackState.Paused; } }
        public bool Playing { get { return output != null && output.PlaybackState == PlaybackState.Playing; } }
        public TimeSpan Position { get { return reader == null ? TimeSpan.Zero : reader.CurrentTime; } }
        public TimeSpan Duration { get { return reader == null ? TimeSpan.Zero : reader.TotalTime; } }
        public float Volume { get { return volume; } set { volume = Math.Max(0, Math.Min(1, value)); if (channel != null) channel.Volume = volume; } }
        public Exception TakeError() { var error = playbackError; playbackError = null; return error; }
        public void Play(string path)
        {
            Dispose(); playbackError = null;
            if (!File.Exists(path)) throw new IOException("Dainos failas nepasiekiamas. Patikrink, ar iPod prijungtas.");
            try
            {
                reader = new MediaFoundationReader(path);
                channel = new WaveChannel32(reader) { PadWithZeroes = false, Volume = volume };
                output = new WasapiOut(AudioClientShareMode.Shared, false, 150);
                var current = output;
                output.PlaybackStopped += delegate(object sender, StoppedEventArgs e) { if (ReferenceEquals(output, current)) playbackError = e.Exception; };
                output.Init(channel); output.Play();
            }
            catch (Exception e) { Dispose(); throw new IOException("Nepavyko paleisti dainos. Patikrink Windows garso išvestį ir failo formatą.\n" + e.Message, e); }
        }
        public void PauseResume()
        {
            if (output == null) return;
            if (Playing) output.Pause();
            else { if (Position >= Duration) reader.CurrentTime = TimeSpan.Zero; output.Play(); }
        }
        public void Seek(double fraction)
        {
            if (reader == null || output == null) return;
            bool resume = Playing; output.Pause();
            reader.CurrentTime = TimeSpan.FromSeconds(Duration.TotalSeconds * Math.Max(0, Math.Min(0.999, fraction)));
            if (resume) output.Play();
        }
        public void Dispose()
        {
            var old = output; output = null;
            if (old != null) { try { old.Stop(); } finally { old.Dispose(); } }
            if (channel != null) { channel.Dispose(); channel = null; reader = null; }
            else if (reader != null) { reader.Dispose(); reader = null; }
        }
    }
}
