using System;

namespace SolarMajesty
{
    /// <summary>
    /// Minimal RIFF/WAVE reader for live voice lines from the local TTS server. Pure; tested by
    /// VoiceTests. Unity can only decode audio files it imported, so audio that arrives over HTTP
    /// has to be turned into samples by hand before AudioClip.SetData.
    /// </summary>
    public static class WavDecoder
    {
        /// <summary>Largest reply accepted, so a misbehaving server cannot make us allocate forever.</summary>
        public const int MaxBytes = 8 * 1024 * 1024;

        public static bool TryDecode(byte[] wav, out float[] samples, out int channels, out int sampleRate)
        {
            samples = null;
            channels = 0;
            sampleRate = 0;
            if (wav == null || wav.Length < 44 || wav.Length > MaxBytes) return false;
            if (!Tag(wav, 0, "RIFF") || !Tag(wav, 8, "WAVE")) return false;

            int format = 0, bits = 0, dataStart = -1, dataLength = 0;
            int pos = 12;
            while (pos + 8 <= wav.Length)
            {
                int size = (int)ReadU32(wav, pos + 4);
                int body = pos + 8;
                if (size < 0 || body + size > wav.Length)
                {
                    // Streaming writers leave the data size as 0 or 0xFFFFFFFF; take what's there.
                    if (Tag(wav, pos, "data")) size = wav.Length - body;
                    else return false;
                }

                if (Tag(wav, pos, "fmt ") && size >= 16)
                {
                    format = ReadU16(wav, body);
                    channels = ReadU16(wav, body + 2);
                    sampleRate = (int)ReadU32(wav, body + 4);
                    bits = ReadU16(wav, body + 14);
                    if (format == 0xFFFE && size >= 40) format = ReadU16(wav, body + 24); // WAVE_FORMAT_EXTENSIBLE
                }
                else if (Tag(wav, pos, "data"))
                {
                    dataStart = body;
                    dataLength = size;
                    break;
                }
                pos = body + size + (size & 1);
            }

            if (dataStart < 0 || channels < 1 || channels > 2 || sampleRate < 8000 || sampleRate > 96000)
                return false;

            if (format == 1 && bits == 16)
            {
                int count = dataLength / 2;
                samples = new float[count];
                for (int i = 0; i < count; i++)
                    samples[i] = (short)ReadU16(wav, dataStart + i * 2) / 32768f;
                return true;
            }
            if (format == 3 && bits == 32)
            {
                int count = dataLength / 4;
                samples = new float[count];
                for (int i = 0; i < count; i++)
                    samples[i] = BitConverter.ToSingle(wav, dataStart + i * 4);
                return true;
            }
            return false;
        }

        private static bool Tag(byte[] b, int at, string tag) =>
            at + 4 <= b.Length && b[at] == tag[0] && b[at + 1] == tag[1] && b[at + 2] == tag[2] && b[at + 3] == tag[3];

        private static int ReadU16(byte[] b, int at) => b[at] | (b[at + 1] << 8);

        private static uint ReadU32(byte[] b, int at) =>
            (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));
    }
}
