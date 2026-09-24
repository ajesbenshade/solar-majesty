using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SolarMajesty
{
    /// <summary>One labelled option offered to Laya. Label is the key it answers with.</summary>
    public struct LayaOption
    {
        public string Label;
        public string Description;
    }

    /// <summary>One question in a multi-question request. Type is "choice" or "noul".</summary>
    public struct LayaQuestion
    {
        public string Id;
        public string Type;
        public string Instructions;
        /// <summary>Choice options; ignored for noul.</summary>
        public IReadOnlyList<LayaOption> Options;
    }

    /// <summary>Parsed answer to a single Laya <c>choice</c> (or <c>noul</c>) question.</summary>
    public sealed class LayaChoice
    {
        public string Choice;
        public float Confidence;
        /// <summary>P(true) for a noul question; NaN otherwise.</summary>
        public float Noul = float.NaN;
        public readonly Dictionary<string, float> Probabilities = new Dictionary<string, float>();
    }

    /// <summary>
    /// Wire format for Laya's <c>POST /v1/systemone</c> (Jev-compatible). Both upstream
    /// <c>laya-serve</c> (PyTorch: Windows / Linux / CUDA) and <c>Tools/laya_sidecar</c>
    /// (laya-mlx on Apple Silicon) speak it. Pure C# — no Unity or network dependency.
    /// </summary>
    public static class LayaProtocol
    {
        public const string Route = "/v1/systemone";
        public const string QuestionId = "action";

        /// <summary>One <c>choice</c> question over <paramref name="options"/>.</summary>
        public static string BuildChoiceRequest(
            string state, string instructions, IReadOnlyList<LayaOption> options)
        {
            var sb = new StringBuilder(256 + options.Count * 64);
            sb.Append("{\"state\":");
            AppendString(sb, state);
            sb.Append(",\"questions\":{");
            AppendString(sb, QuestionId);
            sb.Append(":{\"type\":\"choice\",\"instructions\":");
            AppendString(sb, instructions);
            sb.Append(",\"criteria\":{");
            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendString(sb, options[i].Label);
                sb.Append(':');
                AppendString(sb, options[i].Description ?? "");
            }
            sb.Append("}}}}");
            return sb.ToString();
        }

        /// <summary>Several typed questions over one state (e.g. reading flag orders).</summary>
        public static string BuildRequest(string state, IReadOnlyList<LayaQuestion> questions)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"state\":");
            AppendString(sb, state);
            sb.Append(",\"questions\":{");
            for (int q = 0; q < questions.Count; q++)
            {
                var question = questions[q];
                if (q > 0) sb.Append(',');
                AppendString(sb, question.Id);
                sb.Append(":{\"type\":");
                AppendString(sb, question.Type);
                sb.Append(",\"instructions\":");
                AppendString(sb, question.Instructions);
                if (question.Type == "choice" && question.Options != null)
                {
                    sb.Append(",\"criteria\":{");
                    for (int i = 0; i < question.Options.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        AppendString(sb, question.Options[i].Label);
                        sb.Append(':');
                        AppendString(sb, question.Options[i].Description ?? "");
                    }
                    sb.Append('}');
                }
                sb.Append('}');
            }
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>Reads every answer by question id. False if the envelope is malformed.</summary>
        public static bool TryParseAnswers(string json, out Dictionary<string, LayaChoice> answers)
        {
            answers = null;
            if (!TryAnswersObject(json, out var raw)) return false;
            answers = new Dictionary<string, LayaChoice>();
            foreach (var kv in raw)
                if (kv.Value is Dictionary<string, object> a && TryReadAnswer(a, out var c))
                    answers[kv.Key] = c;
            return true;
        }

        /// <summary>Reads <c>answers.action</c>. False on any malformed or non-finite answer.</summary>
        public static bool TryParseChoice(string json, out LayaChoice result)
        {
            result = null;
            if (!TryAnswersObject(json, out var answers) ||
                !answers.TryGetValue(QuestionId, out var q) || !(q is Dictionary<string, object> answer))
                return false;
            if (!TryReadAnswer(answer, out var choice) || string.IsNullOrEmpty(choice.Choice)) return false;
            result = choice;
            return true;
        }

        static bool TryAnswersObject(string json, out Dictionary<string, object> answers)
        {
            answers = null;
            if (string.IsNullOrEmpty(json)) return false;
            object root;
            try { root = LocalJson.Parse(json); }
            catch (System.FormatException) { return false; }
            if (!(root is Dictionary<string, object> top) ||
                !top.TryGetValue("answers", out var a) || !(a is Dictionary<string, object> dict))
                return false;
            answers = dict;
            return true;
        }

        static bool TryReadAnswer(Dictionary<string, object> answer, out LayaChoice result)
        {
            result = null;
            var choice = new LayaChoice();
            if (answer.TryGetValue("probabilities", out var p) && p is Dictionary<string, object> probs)
            {
                foreach (var kv in probs)
                {
                    if (!(kv.Value is double d) || double.IsNaN(d) || d < 0 || d > 1) return false;
                    choice.Probabilities[kv.Key] = (float)d;
                }
            }
            if (answer.TryGetValue("choice", out var c) && c is string s) choice.Choice = s;
            if (answer.TryGetValue("confidence", out var cf) && cf is double conf) choice.Confidence = (float)conf;
            if (answer.TryGetValue("noul", out var n) && n is double pn)
            {
                if (double.IsNaN(pn) || pn < 0 || pn > 1) return false;
                choice.Noul = (float)pn;
                result = choice;
                return true;
            }

            if (string.IsNullOrEmpty(choice.Choice) || choice.Probabilities.Count == 0) return false;
            result = choice;
            return true;
        }

        static void AppendString(StringBuilder sb, string s) => LocalJson.AppendString(sb, s);
    }
}
