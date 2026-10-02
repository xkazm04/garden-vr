using UnityEngine;

namespace Fidelity
{
    /// <summary>
    /// Drives one hero object's state. The editor builder (FidelityTools) wires the references; captures set the state
    /// and call Apply(); in the headset gallery Update() plays the loop (breath -> answer for the jar, pinch -> halo for the dial).
    /// </summary>
    public sealed class FidelityHero : MonoBehaviour
    {
        public enum Kind { NightJar, DrawnDial }
        public Kind kind;
        public bool autoPlay;

        [Header("Shared state")]
        [Range(0, 1)] public float breath;   // jar: breath-ring fill (one breath = 0 -> 1)
        [Range(0, 1)] public float uncoil;   // jar: fiddlehead uncoil
        [Range(0, 1)] public float answer;   // jar: 'the frond stays'; dial: the pinch halo
        [Range(0, 1)] public float fog = 0.3f;
        public float time;

        [Header("Jar")]
        public MeshFilter fiddle; public Mesh[] fiddleStates;   // uncoil 0, .25, .5, .75, 1 - authored in Blender, same topology
        Vector3[][] _vs; Vector3[] _vw; Mesh _fm;
        public Renderer newFrond, dew;
        public Material ringMat, glassMat, mossMat, coilHaloMat, jarHaloMat, newFrondMat;
        public Transform[] mist;
        public ParticleSystem spores;

        [Header("Dial")]
        public Renderer halo;
        public Material haloMat;
        public Material[] boilMats;
        public Transform[] billboards;   // face the camera fully (halo cards, mist)
        public Transform[] uprightCards; // turn about Y only (drawn plants)


        public void Apply()
        {
            foreach (var m in boilMats ?? new Material[0]) if (m) m.SetFloat("_T", time);
            if (kind == Kind.NightJar) ApplyJar(); else ApplyDial();
        }

        void ApplyJar()
        {
            if (fiddle && fiddleStates != null && fiddleStates.Length > 1)
            {
                if (_fm == null) { _fm = Instantiate(fiddleStates[0]); _fm.name = "FiddleLive"; fiddle.sharedMesh = _fm; _vs = System.Array.ConvertAll(fiddleStates, m => m.vertices); _vw = new Vector3[_vs[0].Length]; }
                float u = Mathf.Clamp01(uncoil) * (fiddleStates.Length - 1); int i0 = Mathf.Min((int)u, fiddleStates.Length - 2); float k = u - i0;
                for (int i = 0; i < _vw.Length; i++) _vw[i] = Vector3.LerpUnclamped(_vs[i0][i], _vs[i0 + 1][i], k);
                _fm.vertices = _vw; _fm.RecalculateNormals(); _fm.RecalculateBounds();
                fiddle.GetComponent<Renderer>().enabled = answer < 0.5f;
            }
            // the answer: the uncoiled fiddlehead is replaced by the frond that stays, brightest in the jar, a dew bead on its tip
            if (newFrond) newFrond.enabled = answer >= 0.5f;
            if (dew) dew.enabled = answer >= 0.5f;
            float pulse = answer > 0 ? Mathf.Exp(-Mathf.Pow((answer - 0.75f) / 0.2f, 2)) : 0;
            if (ringMat) { ringMat.SetFloat("_Fill", answer > 0 ? 1f : breath); ringMat.SetColor("_Color", Color.Lerp(new Color(0.75f, 1.35f, 1.05f), new Color(1.3f, 1.8f, 1.4f), pulse)); }
            if (glassMat) glassMat.SetFloat("_Fog", fog);
            if (mossMat) mossMat.SetColor("_Emission", new Color(0.10f, 0.42f, 0.20f) * (1f + 1.2f * pulse));
            if (coilHaloMat) coilHaloMat.SetColor("_Color", new Color(0.30f, 0.85f, 0.45f) * (answer > 0 ? 0.45f + 0.5f * pulse : 0.55f + 0.25f * Mathf.Sin(breath * Mathf.PI)));
            if (jarHaloMat) jarHaloMat.SetColor("_Color", new Color(0.05f, 0.22f, 0.15f) * (1f + 0.6f * pulse + 0.25f * Mathf.Sin(breath * Mathf.PI)));
            if (newFrondMat) newFrondMat.SetColor("_Emission", new Color(0.35f, 1.0f, 0.62f) * (0.9f + 0.6f * pulse));
            if (mist != null)
                for (int i = 0; i < mist.Length; i++)
                {
                    float ph = Mathf.Repeat(time * 0.08f + i / (float)mist.Length, 1f);
                    mist[i].localPosition = new Vector3(Mathf.Sin(i * 2.1f + time * 0.3f) * 0.006f, 0.165f + ph * 0.10f, 0);
                    mist[i].localScale = new Vector3(0.022f + ph * 0.05f, 0.06f + ph * 0.07f, 1);
                }
        }

        void ApplyDial()
        {
            if (halo) halo.enabled = answer > 0.01f;
            if (haloMat)
            {
                float a = Mathf.Clamp01(answer);
                float breathe = 0.85f + 0.15f * Mathf.Sin(time * 4f);
                haloMat.SetColor("_Color", new Color(1.6f, 1.25f, 0.55f) * a * breathe);
                haloMat.SetColor("_Color2", new Color(0.55f, 0.40f, 0.12f) * a * breathe);
            }
        }

        public void Face(Camera cam)
        {
            if (cam == null) return;
            foreach (var b in billboards ?? new Transform[0])
                if (b) b.rotation = Quaternion.LookRotation(b.position - cam.transform.position, cam.transform.up);
            foreach (var b in uprightCards ?? new Transform[0])
            {
                if (!b) continue;
                var d = b.position - cam.transform.position; d.y = 0;
                b.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
        }

        void Update()
        {
            if (!autoPlay) return;
            time += Time.deltaTime;
            if (kind == Kind.NightJar)
            {
                // six 8-second breaths, then the answer for 6 s, then again
                float cycle = Mathf.Repeat(time, 54f);
                if (cycle < 48f) { breath = Mathf.Repeat(cycle, 8f) / 8f; uncoil = cycle / 48f * 0.75f; answer = 0; fog = 0.2f + 0.3f * Mathf.Sin(breath * Mathf.PI); }
                else { answer = 0.5f + (cycle - 48f) / 12f; uncoil = 1; }
            }
            else answer = Mathf.Repeat(time, 6f) < 3f ? Mathf.Clamp01(Mathf.Repeat(time, 6f) * 3f) : 0;
            Face(Camera.main);
            Apply();
        }
    }
}
