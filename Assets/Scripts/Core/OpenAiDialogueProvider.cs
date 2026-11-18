using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PotionShop.Data;
using UnityEngine;

namespace PotionShop
{
    internal interface IDialogueProvider
    {
        UniTask<(DialogueOption Symptoms, IReadOnlyDictionary<string, string> Reactions)> GenerateAsync(
            GeneratedOrder order, int targetLength, CancellationToken token);
    }

    internal sealed class DialogueProviderException : Exception
    {
        internal DialogueProviderException(string reason) : base(reason) => Reason = reason;
        internal string Reason { get; }
    }

    internal sealed class OpenAiDialogueProvider : IDialogueProvider, IDisposable
    {
        internal static readonly TimeSpan PreparationLimit = TimeSpan.FromSeconds(10);
        private const string Endpoint = "http://127.0.0.1:8765/v1/order-dialogue";
        private const int SchemaVersion = 1;
        private const string DataRevision = "mvp-2026-11-18";
        private static readonly Regex Whitespace = new(@"\s+");
        private static readonly Regex AnswerId = new(@"(?<![A-Za-z0-9])(?:DIS_\d{2}|POT_\d{2}|[A-F][1-3])(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] ReactionKeys = FallbackLine.ReactionKinds.Keys
            .Where(key => key != "timeout").ToArray();

        private readonly HttpClient client = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = 16 * 1024
        };

        public async UniTask<(DialogueOption Symptoms, IReadOnlyDictionary<string, string> Reactions)> GenerateAsync(
            GeneratedOrder order, int targetLength, CancellationToken token)
        {
            string requestId = Guid.NewGuid().ToString("N");
            var input = new
            {
                requestId,
                schemaVersion = SchemaVersion,
                dataRevision = DataRevision,
                character = new
                {
                    code = order.Customer.Id, name = order.Voice.DisplayName, job = order.Customer.Job,
                    speechStyle = order.Voice.SpeechStyle, personality = order.Voice.Personality,
                    chatTopics = order.Voice.ChatTopics
                },
                disease = new
                {
                    id = order.Disease.Id, name = order.Disease.DisplayName, symptomMeanings = order.Disease.SymptomMeanings,
                    lineExamples = order.Disease.LineExamples, bannedWords = order.Disease.BannedWords
                },
                symptomTargetLength = targetLength,
                examples = new
                {
                    symptom = string.Concat(order.Dialogue.Segments.Select(segment =>
                        segment.IsSymptom ? $"<sym>{segment.Text}</sym>" : segment.Text)),
                    reactions = ReactionKeys.ToDictionary(key => key, order.GetReactionLine, StringComparer.Ordinal)
                }
            };

            using var content = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json");
            try
            {
                using var response = await client.PostAsync(Endpoint, content, token);
                if (!response.IsSuccessStatusCode)
                    throw new DialogueProviderException($"relay_http_{(int)response.StatusCode}");
                string body = await response.Content.ReadAsStringAsync();
                token.ThrowIfCancellationRequested();
                return Parse(body, requestId, order.Disease, targetLength);
            }
            catch (HttpRequestException)
            {
                throw new DialogueProviderException("relay_transport");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new DialogueProviderException("relay_transport");
            }
            catch (JsonException)
            {
                throw new DialogueProviderException("invalid_json");
            }
            catch (ArgumentException)
            {
                throw new DialogueProviderException("invalid_unicode");
            }
        }

        private static (DialogueOption, IReadOnlyDictionary<string, string>) Parse(
            string body, string requestId, DiseaseEntry disease, int targetLength)
        {
            using var reader = new JsonTextReader(new StringReader(body)) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 };
            var envelope = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read() || envelope["requestId"]?.Type != JTokenType.String ||
                (string)envelope["requestId"] != requestId || envelope["schemaVersion"]?.Type != JTokenType.Integer ||
                (long)envelope["schemaVersion"] != SchemaVersion || envelope["lines"] is not JObject lines ||
                lines.Count != ReactionKeys.Length + 1)
                throw new DialogueProviderException("invalid_envelope");

            string symptom = ReadLine(lines, "symptom");
            if (!FallbackLine.TryParseSymptoms(symptom, out var dialogue) || !FallbackLine.IsValidSymptom(dialogue, disease))
                throw new DialogueProviderException("invalid_symptom_spans");
            string visibleSymptom = string.Concat(dialogue.Segments.Select(segment => segment.Text));
            if (ContainsAnswer(visibleSymptom, disease.DisplayName) ||
                disease.BannedWords.Any(word => ContainsNormalized(visibleSymptom, word)))
                throw new DialogueProviderException("symptom_banned_word");

            WarnLength("symptom", visibleSymptom, (int)(targetLength * 0.8), (int)(targetLength * 1.2), 300);
            var reactions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in ReactionKeys)
            {
                string line = ReadLine(lines, key);
                if (!FallbackLine.IsPlainText(line) || ContainsAnswer(line, disease.DisplayName))
                    throw new DialogueProviderException("invalid_reaction_text");
                bool receive = FallbackLine.ReactionKinds[key] == FallbackKind.Receive;
                WarnLength(key, line, receive ? 30 : 40, receive ? 60 : 80, 120);
                reactions.Add(key, line);
            }
            return (dialogue, new ReadOnlyDictionary<string, string>(reactions));
        }

        private static string ReadLine(JObject lines, string key)
        {
            var value = lines[key];
            if (value?.Type != JTokenType.String || value.Value<string>() is not string line ||
                string.IsNullOrWhiteSpace(line) || line.Any(char.IsControl))
                throw new DialogueProviderException("invalid_line");
            return line.Trim().Normalize();
        }

        private static void WarnLength(string key, string line, int min, int max, int limit)
        {
            int length = new StringInfo(line.Trim()).LengthInTextElements;
            if (length > limit)
                throw new DialogueProviderException("line_too_long");
            if (length < min || length > max)
                Debug.Log($"[Order dialogue] {key} length {length}, target {min}–{max}.");
        }

        private static bool ContainsAnswer(string line, string diseaseName) =>
            AnswerId.IsMatch(line) || ContainsNormalized(line, diseaseName);

        private static bool ContainsNormalized(string line, string word) =>
            !string.IsNullOrWhiteSpace(word) &&
            Whitespace.Replace(line.Normalize(), " ").IndexOf(
                Whitespace.Replace(word.Trim().Normalize(), " "), StringComparison.Ordinal) >= 0;

        public void Dispose() => client.Dispose();
    }
}
