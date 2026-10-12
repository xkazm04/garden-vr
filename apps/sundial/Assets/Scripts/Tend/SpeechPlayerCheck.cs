#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>Opt-in standalone device check. Only this development harness logs final text.
    /// --speech-check: manual Space/speak/release; --speech-probe: scripted intent/device startup check.</summary>
    public sealed class SpeechPlayerCheck : MonoBehaviour
    {
        static bool Has(string value) => Array.IndexOf(Environment.GetCommandLineArgs(), value) >= 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Application.isEditor || (!Has("--speech-check") && !Has("--speech-probe"))) return;
            SundialController.SaveDirectoryOverride = Path.Combine(Application.temporaryCachePath, "speech-check", Guid.NewGuid().ToString("N"));
            var go = new GameObject("SpeechPlayerCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<SpeechPlayerCheck>();
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var controller = UnityEngine.Object.FindAnyObjectByType<SundialController>();
            if (controller == null) { Debug.Log("[SpeechCheck] No controller"); Application.Quit(2); yield break; }
            var speech = controller.GetComponent<SpeechController>();
            int finals = 0;
            speech.Final += text => { finals++; Debug.Log("[SpeechCheck] Final: " + text); };
            if (Has("--speech-probe"))
            {
                var source = gameObject.AddComponent<ScriptedIntentSource>();
                source.Load("{\"t\":1,\"intent\":\"PinchHold\",\"dur\":4}\n{\"t\":5,\"intent\":\"Release\"}\n");
                speech.SetSource(source);
                Debug.Log("[SpeechCheck] Automated device probe, no human speech claimed");
            }
            else Debug.Log("[SpeechCheck] Hold Space, speak, release. Auto-exit after 30 seconds.");
            speech.Arm();
            yield return new WaitForSecondsRealtime(Has("--speech-probe") ? 8 : 30);
            Debug.Log("[SpeechCheck] Available=" + speech.Available + " Listening=" + speech.Listening + " Finals=" + finals);
            speech.CompleteCoachCall();
            Debug.Log("[SpeechCheck] Transient text cleared=" + (speech.Transcript.Length == 0));
            Application.Quit();
        }
    }
}
#endif
