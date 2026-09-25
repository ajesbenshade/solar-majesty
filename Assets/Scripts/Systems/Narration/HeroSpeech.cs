using System;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Which TTS voice a hero speaks with, and how.</summary>
    public struct HeroVoiceSpec
    {
        /// <summary>Kokoro voice id (am_* / af_* American, bm_* / bf_* British).</summary>
        public string Voice;
        /// <summary>1 = normal. Workaholics talk fast, lazy heroes drawl.</summary>
        public float Speed;
        /// <summary>0 = clean human voice, 1 = full radio/vocoder treatment for machines.</summary>
        public float Robot;
    }

    /// <summary>
    /// Spoken hero lines (text-to-speech on a local Kokoro server). Pure: voice casting, the
    /// OpenAI-style <c>/v1/audio/speech</c> request, WAV decoding and the robot radio filter.
    /// Runtime playback lives in <c>HeroSpeaker</c>.
    /// </summary>
    public static class HeroSpeech
    {
        public const string Route = "/v1/audio/speech";
        /// <summary>Lines longer than this in seconds are not played (they would talk over the game).</summary>
        public const float MaxSeconds = 5f;

        /// <summary>Only the Medic is human; everyone else is a machine and gets the radio voice.</summary>
        public static bool IsMachine(SpecialistClass cls) => cls != SpecialistClass.Medic;

        /// <summary>
        /// Casting: each class has two voices so a colony isn't a choir of one; the hero id picks
        /// which. Personality sets pace.
        /// </summary>
        public static HeroVoiceSpec VoiceFor(SpecialistClass cls, int heroId, float workaholic)
        {
            string[] pair = cls switch
            {
                SpecialistClass.DefenseMech => new[] { "am_onyx", "am_fenrir" },
                SpecialistClass.SentinelMech => new[] { "bm_george", "am_fenrir" },
                SpecialistClass.EngineerBot => new[] { "am_michael", "bm_lewis" },
                SpecialistClass.ScoutDrone => new[] { "af_sky", "am_puck" },
                SpecialistClass.Medic => new[] { "af_heart", "bf_emma" },
                SpecialistClass.HarvesterBot => new[] { "am_eric", "af_kore" },
                SpecialistClass.SurveyorBot => new[] { "bf_isabella", "am_liam" },
                SpecialistClass.TerraformerBot => new[] { "am_adam", "bm_daniel" },
                SpecialistClass.CourierBot => new[] { "af_nova", "am_echo" },
                SpecialistClass.GeologistBot => new[] { "bm_fable", "af_sarah" },
                _ => new[] { "am_michael", "af_heart" }
            };
            int pick = (heroId & int.MaxValue) % pair.Length;
            float speed = Mathf.Clamp(0.92f + (workaholic - 0.5f) * 0.3f, 0.8f, 1.15f);
            return new HeroVoiceSpec
            {
                Voice = pair[pick],
                Speed = speed,
                Robot = IsMachine(cls) ? (cls == SpecialistClass.DefenseMech || cls == SpecialistClass.SentinelMech ? 0.8f : 0.55f) : 0f
            };
        }

        /// <summary>OpenAI-style speech request (Kokoro-FastAPI and Tools/local_ai/voice_server.py).</summary>
        public static string BuildRequest(string line, in HeroVoiceSpec v)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"model\":\"kokoro\",\"input\":");
            LocalJson.AppendString(sb, line ?? "");
            sb.Append(",\"voice\":");
            LocalJson.AppendString(sb, string.IsNullOrEmpty(v.Voice) ? "am_michael" : v.Voice);
            sb.Append(",\"speed\":").Append(v.Speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"response_format\":\"wav\"}");
            return sb.ToString();
        }

        /// <summary>
        /// Decodes a PCM WAV (16-bit or 32-bit float, any channel count) into interleaved floats.
        /// Handles the streaming form where the data chunk size is written as 0 / 0xFFFFFFFF.
        /// </summary>
        public static bool TryDecodeWav(byte[] wav, out float[] samples, out int channels, out int sampleRate)
        {
            samples = null;
            channels = 0;
            sampleRate = 0;
            if (wav == null || wav.Length < 44) return false;
            if (!Tag(wav, 0, "RIFF") || !Tag(wav, 8, "WAVE")) return false;

            int format = 0, bits = 0, pos = 12;
            while (pos + 8 <= wav.Length)
            {
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (Tag(wav, pos, "fmt "))
                {
                    if (pos + 24 > wav.Length) return false;
                    format = BitConverter.ToUInt16(wav, pos + 8);
                    channels = BitConverter.ToUInt16(wav, pos + 10);
                    sampleRate = BitConverter.ToInt32(wav, pos + 12);
                    bits = BitConverter.ToUInt16(wav, pos + 22);
                    if (format == 0xFFFE && size >= 40 && pos + 34 <= wav.Length)
                        format = BitConverter.ToUInt16(wav, pos + 32); // WAVE_FORMAT_EXTENSIBLE sub-format
                }
                else if (Tag(wav, pos, "data"))
                {
                    int start = pos + 8;
                    int len = size <= 0 || start + size > wav.Length ? wav.Length - start : size;
                    if (channels <= 0 || sampleRate <= 0) return false;
                    if (format == 1 && bits == 16)
                    {
                        int n = len / 2;
                        samples = new float[n];
                        for (int i = 0; i < n; i++)
                            samples[i] = BitConverter.ToInt16(wav, start + i * 2) / 32768f;
                        return n > 0;
                    }
                    if (format == 3 && bits == 32)
                    {
                        int n = len / 4;
                        samples = new float[n];
                        for (int i = 0; i < n; i++)
                            samples[i] = BitConverter.ToSingle(wav, start + i * 4);
                        return n > 0;
                    }
                    return false; // unsupported encoding
                }
                if (size < 0) return false;
                pos += 8 + size + (size & 1);
            }
            return false;
        }

        /// <summary>
        /// Helmet-radio / vocoder treatment for machine heroes, in place: band-limit to a radio band,
        /// a short metallic comb, a touch of ring modulation, then soft clip. Strength 0 = untouched.
        /// Deterministic and allocation-light so it can run off the main thread.
        /// </summary>
        public static void ApplyRobot(float[] s, int channels, int sampleRate, float strength)
        {
            if (s == null || strength <= 0f || sampleRate <= 0) return;
            strength = Mathf.Clamp01(strength);
            channels = Mathf.Max(1, channels);

            // One-pole high-pass (~300 Hz) and low-pass (~3.4 kHz): the classic radio band.
            float hpA = Mathf.Exp(-2f * Mathf.PI * 300f / sampleRate);
            float lpA = Mathf.Exp(-2f * Mathf.PI * 3400f / sampleRate);
            int comb = Mathf.Max(1, Mathf.RoundToInt(0.0062f * sampleRate)) * channels; // ~6 ms metallic ring
            float feedback = 0.32f * strength;
            float ring = 0.22f * strength;
            float ringW = 2f * Mathf.PI * 62f / sampleRate;
            var delay = new float[comb];
            int di = 0;
            float hpPrevIn = 0f, hpPrevOut = 0f, lp = 0f;
            float peak = 1e-6f;

            for (int i = 0; i < s.Length; i++)
            {
                float x = s[i];
                float hp = hpA * (hpPrevOut + x - hpPrevIn);
                hpPrevIn = x;
                hpPrevOut = hp;
                lp = lp + (1f - lpA) * (hp - lp);
                float band = lp;

                float d = delay[di];
                float y = band + d * feedback;
                delay[di] = y;
                di = (di + 1) % comb;

                int frame = i / channels;
                y *= 1f - ring + ring * Mathf.Cos(ringW * frame);

                float wet = Mathf.Lerp(x, y * 1.35f, strength);
                s[i] = wet;
                float a = Mathf.Abs(wet);
                if (a > peak) peak = a;
            }

            // Normalise to the original loudness ceiling, then soft clip so nothing crackles.
            float gain = peak > 0.95f ? 0.95f / peak : 1f;
            for (int i = 0; i < s.Length; i++)
            {
                float v = s[i] * gain;
                s[i] = v / (1f + Mathf.Abs(v) * 0.15f);
            }
        }

        static bool Tag(byte[] b, int at, string tag) =>
            at + 4 <= b.Length && b[at] == tag[0] && b[at + 1] == tag[1] && b[at + 2] == tag[2] && b[at + 3] == tag[3];
    }
}
