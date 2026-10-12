using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>B5/B7 entry point. Read the current save on every call; opt-out never constructs a client.</summary>
    public sealed class SundialCoach
    {
        readonly Func<SundialSave> _save;
        readonly RelayConfig _config;
        readonly Func<string, ICoachClient> _createClient;

        public SundialCoach(Func<SundialSave> save, RelayConfig config, Func<string, ICoachClient> createClient = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _config = config;
            _createClient = createClient ?? CreateClient;
        }

        ICoachClient CreateClient(string key)
        {
#if UNITY_EDITOR_WIN
            if (_config != null && _config.UseEditorCli) return new EditorCliCoachClient();
#endif
            return _config == null ? null : new RelayCoachClient(_config.RelayUrl, key);
        }

        ICoachClient Client(out bool optedIn)
        {
            var save = _save();
            optedIn = save != null && save.Settings != null && save.Settings.CoachOptIn;
            return optedIn ? _createClient(save.InstallKey) : null;
        }

        public Task<OnboardingResult> OnboardAsync(IReadOnlyList<string> answers, CancellationToken cancel = default)
        {
            var client = Client(out bool optedIn);
            return CoachFlow.OnboardAsync(client, optedIn, CoachVoice.Sundial, answers,
                _config == null ? CoachFlow.Budget : _config.Budget, cancel);
        }

        public Task<ReflectionResult> ReflectAsync(string transcript, ReflectionContext context, string stone,
            CancellationToken cancel = default)
        {
            var client = Client(out bool optedIn);
            return CoachFlow.ReflectAsync(client, optedIn, CoachVoice.Sundial, transcript, context, stone,
                _config == null ? CoachFlow.Budget : _config.Budget, cancel);
        }
    }
}
