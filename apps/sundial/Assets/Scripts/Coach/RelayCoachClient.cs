using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using UnityEngine.Networking;

namespace GardenVR.Sundial
{
    /// <summary>Call on Unity's main thread. No prompt, reply or install key is logged or retained.</summary>
    public sealed class RelayCoachClient : ICoachClient
    {
        readonly string _url;
        readonly string _installKey;

        public RelayCoachClient(string relayUrl, string installKey)
        {
            _url = (relayUrl ?? "").TrimEnd('/') + RelayProtocol.Path;
            _installKey = installKey;
        }

        public async Task<CoachReply> AskAsync(CoachRequest request, CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return CoachReply.Failed(CoachFailure.Timeout);
            if (!Uri.TryCreate(_url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
                string.IsNullOrWhiteSpace(_installKey)) return CoachReply.Failed(CoachFailure.Error);
            try
            {
                using (var web = new UnityWebRequest(_url, "POST"))
                {
                    web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(RelayProtocol.Encode(request)));
                    web.downloadHandler = new DownloadHandlerBuffer();
                    web.SetRequestHeader("Content-Type", "application/json");
                    web.SetRequestHeader(RelayProtocol.InstallKeyHeader, _installKey);
                    // A redirect must not forward a transcript or install key to another endpoint.
                    web.redirectLimit = 0;
                    var operation = web.SendWebRequest();
                    while (!operation.isDone)
                    {
                        if (cancel.IsCancellationRequested)
                        {
                            web.Abort();
                            return CoachReply.Failed(CoachFailure.Timeout);
                        }
                        // Keep Unity API calls (including Abort) on Unity's context, even when
                        // CoachFlow cancels its token from a thread-pool timeout continuation.
                        await Task.Yield();
                    }
                    if (cancel.IsCancellationRequested) return CoachReply.Failed(CoachFailure.Timeout);
                    return RelayProtocol.Decode((int)web.responseCode, web.downloadHandler.text);
                }
            }
            catch (Exception) { return CoachReply.Failed(CoachFailure.Error); }
        }
    }
}
