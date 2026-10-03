using System.Collections.Generic;
using GardenVR.Input;
using GardenVR.Room;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class RoomRigTests
    {
        readonly List<Object> _owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
        }

        [Test]
        public void Clamp_LimitsYawTo40AndPitchTo25()
        {
            SeatedRig rig = MakeRig(out FakeHeadPose fake, out Transform pivot);

            fake.LocalRotation = Quaternion.Euler(10f, -18f, 0f);
            rig.ApplyPose();
            Assert.AreEqual(-18f, rig.YawDegrees, 0.05f);
            Assert.AreEqual(10f, rig.PitchDegrees, 0.05f);

            fake.LocalRotation = Quaternion.Euler(80f, 120f, 0f);
            rig.ApplyPose();
            Assert.AreEqual(40f, rig.YawDegrees, 0.05f);
            Assert.AreEqual(25f, rig.PitchDegrees, 0.05f);
            AssertLookOffset(pivot, 25f, 40f);

            fake.LocalRotation = Quaternion.Euler(-80f, -120f, 0f);
            rig.ApplyPose();
            Assert.AreEqual(-40f, rig.YawDegrees, 0.05f);
            Assert.AreEqual(-25f, rig.PitchDegrees, 0.05f);
            AssertLookOffset(pivot, -25f, -40f);
        }

        [Test]
        public void Recentre_ClearsTheHeadOffset()
        {
            SeatedRig rig = MakeRig(out FakeHeadPose fake, out Transform pivot);
            fake.LocalRotation = Quaternion.Euler(12f, -18f, 0f);
            rig.ApplyPose();
            Assert.AreEqual(-18f, rig.YawDegrees, 0.05f);
            Assert.AreEqual(12f, rig.PitchDegrees, 0.05f);

            fake.LocalRotation = Quaternion.identity;
            fake.RaiseRecentred();
            Assert.AreEqual(0f, rig.YawDegrees, 0.001f);
            Assert.AreEqual(0f, rig.PitchDegrees, 0.001f);
            Assert.Less(Quaternion.Angle(pivot.localRotation, SeatedRig.SeatedPitchQuaternion), 0.05f);

            rig.ApplyPose();
            Assert.AreEqual(0f, rig.YawDegrees, 0.001f);
            Assert.AreEqual(0f, rig.PitchDegrees, 0.001f);
        }

        [Test]
        public void DeskPose_ComesFromHeightDistanceAndLateral()
        {
            var go = new GameObject("desk");
            _owned.Add(go);
            var desk = go.AddComponent<PcDeskAnchor>();
            desk.Height = PcDeskAnchor.DefaultHeight;
            desk.Distance = PcDeskAnchor.TerrariumDistance;
            desk.Lateral = -0.05f;

            Pose pose = desk.DeskPose;
            Assert.AreEqual(-0.05f, pose.position.x, 0.0001f);
            Assert.AreEqual(0.75f, pose.position.y, 0.0001f);
            Assert.AreEqual(0.40f, pose.position.z, 0.0001f);
            Assert.AreEqual(SeatedRig.DefaultEyeHeight - PcDeskAnchor.BelowEye, pose.position.y, 0.0001f);
            Assert.AreEqual(0f, Quaternion.Angle(pose.rotation, Quaternion.identity), 0.001f);

            desk.Distance = PcDeskAnchor.SundialDistance;
            Assert.AreEqual(0.55f, desk.DeskPose.position.z, 0.0001f);
            Assert.AreEqual(0.75f, desk.DeskPose.position.y, 0.0001f);

            Plane plane = desk.DeskPlane;
            Assert.AreEqual(1f, Vector3.Dot(plane.normal, Vector3.up), 0.0001f);
            Assert.AreEqual(0f, plane.GetDistanceToPoint(desk.DeskPose.position), 0.0001f);
        }

        [Test]
        public void DeskTrace_RaisesPlacedAfterTheNearEdgeFinishes()
        {
            var go = new GameObject("desk");
            _owned.Add(go);
            var desk = go.AddComponent<PcDeskAnchor>();
            Assert.AreEqual(PcDeskAnchor.TraceDelaySeconds, desk.TraceDelay, 0.0001f, "delay default");
            Assert.AreEqual(PcDeskAnchor.TraceSeconds, desk.TraceDuration, 0.0001f, "duration default");
            int placed = 0;
            desk.Placed += () => placed++;
            desk.Begin();
            desk.Tick(desk.TraceDelay);
            Assert.AreEqual(0, placed, "early elapsed=" + desk.Elapsed + " t01=" + desk.Trace01 + " running=" + desk.TraceRunning);
            Assert.AreEqual(0f, desk.Trace01, 0.0001f);

            desk.Tick(desk.TraceDuration);
            Assert.AreEqual(1, placed, "elapsed=" + desk.Elapsed + " delay=" + desk.TraceDelay + " dur=" + desk.TraceDuration + " t01=" + desk.Trace01 + " running=" + desk.TraceRunning);
            Assert.AreEqual(1f, desk.Trace01, 0.0001f);
            desk.Tick(1f);
            Assert.AreEqual(1, placed);
        }

        [Test]
        public void DeskTrace_StaysHiddenUntilTheLineIsDrawing()
        {
            var go = new GameObject("desk");
            _owned.Add(go);
            var desk = go.AddComponent<PcDeskAnchor>();
            var trace = new GameObject("trace");
            _owned.Add(trace);
            trace.transform.SetParent(go.transform, false);
            trace.transform.localScale = new Vector3(0.48f, 0.006f, 1f);
            desk.BindTrace(trace.transform);

            desk.Begin();
            Assert.Less(trace.transform.localScale.x, 0.01f, "hidden before the line starts");

            desk.Tick(desk.TraceDelay + 0.5f * desk.TraceDuration);
            Assert.Greater(trace.transform.localScale.x, 0.1f, "visible while drawing");

            desk.Tick(desk.TraceDuration);
            Assert.Less(trace.transform.localScale.x, 0.01f, "hidden after Placed");
        }

        [Test]
        public void PlateFade_ReachesFullExposureAtFadeSeconds()
        {
            Shader shader = Shader.Find("Fidelity/Plate");
            Assert.IsNotNull(shader, "Fidelity/Plate");

            var go = new GameObject("plate");
            _owned.Add(go);
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(shader);
            _owned.Add(material);
            renderer.sharedMaterial = material;

            var plate = go.AddComponent<PcRoomPlate>();
            Assert.AreEqual(PcRoomPlate.DefaultFadeSeconds, plate.FadeSeconds, 0.0001f);
            plate.Exposure = 0.62f;
            plate.Show(2f);
            Assert.AreEqual(0f, plate.CurrentExposure, 0.0001f);
            Assert.AreEqual(0f, material.GetFloat("_Exposure"), 0.0001f);

            plate.Tick(1f);
            Assert.AreEqual(0.31f, plate.CurrentExposure, 0.0001f);
            Assert.Less(plate.CurrentExposure, plate.Exposure);

            plate.Tick(1f);
            Assert.AreEqual(0.62f, plate.CurrentExposure, 0.0001f);
            Assert.AreEqual(plate.Exposure, material.GetFloat("_Exposure"), 0.0001f);

            plate.Tick(0.5f);
            Assert.AreEqual(0.62f, plate.CurrentExposure, 0.0001f);
        }

        [Test]
        public void RestPitch_AimsTheEyeAtTheDesk()
        {
            SeatedRig rig = MakeRig(out _, out Transform pivot);
            var deskGo = new GameObject("desk");
            _owned.Add(deskGo);
            deskGo.transform.SetParent(rig.transform, false);
            var desk = deskGo.AddComponent<PcDeskAnchor>();
            desk.Height = PcDeskAnchor.DefaultHeight;
            desk.Distance = PcDeskAnchor.SundialDistance;

            var anchorGo = new GameObject("RoomPlateAnchor");
            _owned.Add(anchorGo);
            anchorGo.transform.SetParent(rig.transform, false);
            rig.SetPlateAnchor(anchorGo.transform);

            float expect = Mathf.Atan2(PcDeskAnchor.BelowEye, PcDeskAnchor.SundialDistance) * Mathf.Rad2Deg;
            Assert.AreEqual(expect, rig.RestPitchDegrees, 0.02f);
            rig.ApplyPose();

            Vector3 forward = pivot.rotation * Vector3.forward;
            Vector3 toDesk = desk.transform.position - pivot.position;
            Assert.Less(Vector3.Angle(forward, toDesk), 0.2f);
            Assert.Less(Quaternion.Angle(anchorGo.transform.localRotation, Quaternion.Euler(expect, 0f, 0f)), 0.05f);
            Assert.AreEqual(SeatedRig.DefaultEyeHeight, anchorGo.transform.localPosition.y, 0.0001f);
        }

        [Test]
        public void RestPitch_CanSitTheHeroLowerInTheFrame()
        {
            SeatedRig rig = MakeRig(out _, out Transform pivot);
            var deskGo = new GameObject("desk");
            _owned.Add(deskGo);
            deskGo.transform.SetParent(rig.transform, false);
            var desk = deskGo.AddComponent<PcDeskAnchor>();
            desk.Height = PcDeskAnchor.DefaultHeight;
            desk.Distance = PcDeskAnchor.TerrariumDistance;

            rig.LookAboveDesk = 16f;
            float aim = Mathf.Atan2(PcDeskAnchor.BelowEye, PcDeskAnchor.TerrariumDistance) * Mathf.Rad2Deg;
            Assert.AreEqual(aim - 16f, rig.RestPitchDegrees, 0.05f);
            rig.ApplyPose();

            Vector3 forward = pivot.rotation * Vector3.forward;
            Vector3 toDesk = desk.transform.position - pivot.position;
            Assert.AreEqual(16f, Vector3.Angle(forward, toDesk), 0.2f);
        }

        [Test]
        public void LensCard_FillsTheSeatedLens()
        {
            Vector2 size = SeatedRig.SeatedCardSize(
                SeatedRig.PlateDistance, SeatedRig.DefaultFieldOfView, SeatedRig.SeatedCaptureAspect);
            var card = new GameObject("lens");
            _owned.Add(card);
            SeatedRig.PlaceLensCard(card.transform);
            Assert.AreEqual(SeatedRig.PlateDistance, card.transform.localPosition.z, 0.0001f);
            Assert.AreEqual(size.x * SeatedRig.SeatedCardMargin, card.transform.localScale.x, 0.0001f);
            Assert.AreEqual(size.y * SeatedRig.SeatedCardMargin, card.transform.localScale.y, 0.0001f);
            float half = Mathf.Atan((card.transform.localScale.y * 0.5f) / SeatedRig.PlateDistance) * Mathf.Rad2Deg;
            Assert.Greater(half, SeatedRig.DefaultFieldOfView * 0.5f);
            Assert.Less(half, SeatedRig.DefaultFieldOfView * 0.5f * SeatedRig.SeatedCardMargin + 0.05f);
        }

        [Test]
        public void SeatedCard_FillsTheCaptureFrustum()
        {
            Vector2 size = SeatedRig.SeatedCardSize(
                SeatedRig.PlateDistance, SeatedRig.SeatedCaptureFov, SeatedRig.SeatedCaptureAspect);
            Assert.AreEqual(8f, size.y, 0.0001f);
            Assert.AreEqual(8f * (1824f / 1024f), size.x, 0.0001f);
            float half = Mathf.Atan((size.y * 0.5f) / SeatedRig.PlateDistance) * Mathf.Rad2Deg;
            Assert.AreEqual(SeatedRig.SeatedCaptureFov * 0.5f, half, 0.001f);

            var card = new GameObject("card");
            _owned.Add(card);
            SeatedRig.PlaceSeatedCard(card.transform);
            Assert.AreEqual(SeatedRig.PlateDistance, card.transform.localPosition.z, 0.0001f);
            Assert.AreEqual(size.x * SeatedRig.SeatedCardMargin, card.transform.localScale.x, 0.0001f);
            Assert.AreEqual(size.y * SeatedRig.SeatedCardMargin, card.transform.localScale.y, 0.0001f);
        }

        [Test]
        public void Eye_SitsAtSeatedHeightWithPcLens()
        {
            SeatedRig rig = MakeRig(out _, out Transform pivot);
            Camera camera = pivot.GetComponentInChildren<Camera>();
            rig.ApplyBody();
            Assert.AreEqual(SeatedRig.DefaultEyeHeight, pivot.localPosition.y, 0.0001f);
            Assert.AreEqual(SeatedRig.DefaultFieldOfView, camera.fieldOfView, 0.01f);
            Assert.AreEqual(SeatedRig.DefaultNearClip, camera.nearClipPlane, 0.0001f);
        }

        static void AssertLookOffset(Transform pivot, float pitch, float yaw)
        {
            Quaternion offset = Quaternion.Inverse(SeatedRig.SeatedPitchQuaternion) * pivot.localRotation;
            SeatedRig.ExtractLook(offset, out float gotYaw, out float gotPitch);
            Assert.AreEqual(yaw, gotYaw, 0.05f);
            Assert.AreEqual(pitch, gotPitch, 0.05f);
        }

        SeatedRig MakeRig(out FakeHeadPose fake, out Transform pivot)
        {
            var root = new GameObject("rig");
            _owned.Add(root);
            var pivotGo = new GameObject("HeadPivot");
            pivotGo.transform.SetParent(root.transform, false);
            var eye = new GameObject("EyeCamera");
            eye.transform.SetParent(pivotGo.transform, false);
            eye.AddComponent<Camera>();
            var head = new GameObject("HeadPose");
            head.transform.SetParent(root.transform, false);
            fake = head.AddComponent<FakeHeadPose>();
            var rig = root.AddComponent<SeatedRig>();
            rig.Bind(pivotGo.transform, eye.GetComponent<Camera>(), fake);
            pivot = pivotGo.transform;
            return rig;
        }

        sealed class FakeHeadPose : MonoBehaviour, IHeadPoseSource
        {
            public Quaternion LocalRotation { get; set; } = Quaternion.identity;
            public event System.Action Recentred;
            public void RaiseRecentred() { Recentred?.Invoke(); }
        }
    }
}
