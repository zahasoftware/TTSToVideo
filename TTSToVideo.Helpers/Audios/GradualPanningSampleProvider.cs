using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers.Audios
{

    public class GradualPanningSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly float panDuration;
        private readonly float switchInterval;
        private readonly float totalDuration;
        private float currentTime;
        private int panDirection = 1;

        public WaveFormat WaveFormat => source.WaveFormat;

        public GradualPanningSampleProvider(ISampleProvider source, float panDuration, float switchInterval, float totalDuration)
        {
            this.source = source;
            this.panDuration = panDuration;
            this.switchInterval = switchInterval;
            this.totalDuration = totalDuration;
            this.currentTime = 0;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i += 2)
            {
                float pan = currentTime / panDuration; // Gradually increase pan
                pan = (panDirection == 1) ? pan : 1 - pan; // Flip direction

                buffer[offset + i] *= 1 - pan; // Left channel
                buffer[offset + i + 1] *= pan; // Right channel
                currentTime += 1.0f / WaveFormat.SampleRate;

                if (currentTime >= switchInterval)
                {
                    panDirection *= -1; // Switch direction
                    currentTime = 0; // Reset the timer
                }
            }

            return samplesRead;
        }
    }
}
