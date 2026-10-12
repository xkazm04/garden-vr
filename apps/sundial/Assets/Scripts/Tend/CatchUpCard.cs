using System;
using System.Collections.Generic;
using System.Globalization;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>A launch-only paper list. Core supplies every offered day and owns every late log.</summary>
    [DefaultExecutionOrder(120)]
    public sealed class CatchUpCard : MonoBehaviour
    {
        public const string DoneId = "catchup.done";
        public const int PageSize = 6;
        SundialController _controller;
        Action _complete;
        GameObject _paper;
        Material _ink;
        List<CatchUpItem> _items = new List<CatchUpItem>();
        int _page, _closedFrame = -1;
        public bool Open { get; private set; }
        public bool BlocksInput => Open || _closedFrame == Time.frameCount;
        public IReadOnlyList<CatchUpItem> Items => _items;
        public int Page => _page;
        public static string RowId(CatchUpItem item) => "catchup.row." + item.Day.Index + "." + item.Habit.Id;

        void Awake() { _controller = GetComponent<SundialController>(); }
        public void Offer(Action complete)
        {
            _complete = complete;
            _items = _controller.Service.PendingCatchUp();
            if (_items.Count == 0) { Close(); return; }
            Open = true; _page = 0; Draw();
        }
        public void Handle(HandIntent intent)
        {
            if (!Open) return;
            if (intent.Kind == HandIntentKind.PalmOpen) { Close(); return; }
            if (intent.Kind != HandIntentKind.Pinch) return;
            if (intent.TargetId == DoneId) { Close(); return; }
            if (intent.TargetId == "catchup.next") { _page = Mathf.Min(_page + 1, (_items.Count - 1) / PageSize); Draw(); return; }
            if (intent.TargetId == "catchup.previous") { _page = Mathf.Max(0, _page - 1); Draw(); return; }
            for (int i = _page * PageSize; i < Mathf.Min(_items.Count, (_page + 1) * PageSize); i++)
            {
                if (RowId(_items[i]) != intent.TargetId) continue;
                _controller.Service.LogCatchUp(_items[i]);
                Refresh(); return;
            }
        }
        void Refresh()
        {
            _items = _controller.Service.PendingCatchUp();
            _page = Mathf.Clamp(_page, 0, Mathf.Max(0, (_items.Count - 1) / PageSize));
            Draw();
        }
        void Update()
        {
            if (!Open) return;
            var current = _controller.Service.PendingCatchUp();
            bool changed = current.Count != _items.Count;
            for (int i = 0; !changed && i < current.Count; i++) changed = RowId(current[i]) != RowId(_items[i]);
            if (changed) Refresh();
        }
        void LateUpdate() { if (_paper != null) ReflectionPaper.Face(_paper.transform); }
        public void Close()
        {
            if (Open) _closedFrame = Time.frameCount;
            Open = false; Clear();
            var complete = _complete; _complete = null; complete?.Invoke();
        }
        void OnDisable() { Close(); }
        void OnDestroy() { if (_ink != null) Destroy(_ink); }
        void Clear() { if (_paper != null) { _paper.SetActive(false); Destroy(_paper); _paper = null; } }
        void Draw()
        {
            Clear();
            _paper = ReflectionPaper.Note(transform, "CatchUpCard", "",
                new Vector3(-.035f, _controller.View.faceY + .075f, -.025f), new Vector2(.25f, .21f), false);
            _paper.transform.localScale = Vector3.one * .75f;
            Words("catchup.title", "What happened since?", 0, .057f, .155f, .016f, false);
            Words("catchup.hint", "Pinch what happened. Leave the rest.", 0, .039f, .160f, .012f, false);
            if (_items.Count == 0) Words("catchup.empty", "All noted", 0, 0, .15f, .016f, false);
            for (int i = _page * PageSize; i < Mathf.Min(_items.Count, (_page + 1) * PageSize); i++)
            {
                var item = _items[i];
                string date = item.Day.CivilDate.ToString("ddd d MMM", CultureInfo.InvariantCulture);
                Words(RowId(item), date + "   " + WeekPageInk.PlantLine(item.Habit), 0,
                    .022f - (i % PageSize) * .0125f, .16f, .011f, true);
            }
            if (_page > 0) Words("catchup.previous", "Previous", -.053f, -.060f, .047f, .015f, true);
            if ((_page + 1) * PageSize < _items.Count) Words("catchup.next", "Next", .003f, -.060f, .036f, .015f, true);
            Words(DoneId, "Done", .060f, -.060f, .040f, .015f, true);
        }
        void Words(string id, string text, float x, float y, float width, float height, bool target)
        {
            var go = new GameObject(id); go.transform.SetParent(_paper.transform, false);
            go.transform.localPosition = new Vector3(x, y, -.001f);
            var words = go.AddComponent<TextMesh>(); InkLetter.Apply(words, text, 48, .0024f);
            var renderer = go.GetComponent<Renderer>();
            var bounds = renderer.localBounds.size;
            words.characterSize *= Mathf.Min(1, width / Mathf.Max(.001f, bounds.x), height / Mathf.Max(.001f, bounds.y));
            if (_ink == null) _ink = new Material(renderer.sharedMaterial) { renderQueue = 3401 };
            renderer.sharedMaterial = _ink;
            if (!target) return;
            go.AddComponent<BoxCollider>().size = new Vector3(width, height, .003f);
            go.AddComponent<IntentTarget>().Id = id;
        }
    }
}
