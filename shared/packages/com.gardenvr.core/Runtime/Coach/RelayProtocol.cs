using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// The wire format between the app and the coach relay (tools/relay). The app POSTs <see cref="Encode"/> to
    /// /v1/coach with header x-install-key: <see cref="NewInstallKey"/>, and reads the answer with <see cref="Decode"/>.
    /// The relay accepts only the system prompts listed by <see cref="AllowedSystems"/>.
    /// </summary>
    public static class RelayProtocol
    {
        public const string Path = "/v1/coach";
        public const string InstallKeyHeader = "x-install-key";

        public static string Encode(CoachRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var obj = new JsonObject();
            obj.Set("kind", JsonValue.String(request.Kind.ToString()));
            obj.Set("system", JsonValue.String(request.System ?? ""));
            obj.Set("user", JsonValue.String(request.User ?? ""));
            obj.Set("maxTokens", JsonValue.Number(request.MaxTokens));
            return Json.Write(obj);
        }

        /// <summary>
        /// The reply for an HTTP status and body. A 200 with ok and a text is a success; "Refused" is a refusal; anything
        /// else (a rate limit, a rejection, a server error, a body that does not read) is an error.
        /// </summary>
        public static CoachReply Decode(int status, string body)
        {
            JsonObject obj;
            try { obj = string.IsNullOrEmpty(body) ? null : Json.ParseObject(body); }
            catch (FormatException) { obj = null; }
            catch (InvalidOperationException) { obj = null; }
            catch (ArgumentException) { obj = null; }
            if (obj == null) return CoachReply.Failed(CoachFailure.Error);
            bool ok = obj.Has("ok") && obj.Get("ok").Kind == JsonKind.Bool && obj.Get("ok").AsBool();
            if (status == 200 && ok && obj.Has("text") && obj.Get("text").Kind == JsonKind.String)
                return CoachReply.Success(obj.Get("text").AsString());
            string failure = obj.Has("failure") && obj.Get("failure").Kind == JsonKind.String ? obj.Get("failure").AsString() : null;
            return CoachReply.Failed(failure == "Refused" ? CoachFailure.Refused : CoachFailure.Error);
        }

        /// <summary>A per-install key: 32 hex characters, random, kept in the save. It identifies nobody; it only rate-limits.</summary>
        public static string NewInstallKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>Every system prompt core can send, one per voice; the relay's allowed-systems.json is written from this.</summary>
        public static List<string> AllowedSystems()
        {
            var systems = new List<string>();
            foreach (CoachVoice voice in new[] { CoachVoice.Terrarium, CoachVoice.Sundial })
            {
                foreach (CoachRequest r in new[] { CoachPrompts.Onboarding(voice, null), CoachPrompts.Reflection(voice, "", null) })
                    if (!systems.Contains(r.System)) systems.Add(r.System);
            }
            return systems;
        }
    }
}
