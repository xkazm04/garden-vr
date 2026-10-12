using System;
using System.Collections.Generic;
using System.IO;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class SpeechSourceTests
    {
        ScriptedSpeechSource _speech;
        List<string> _events;
        [SetUp] public void Setup()
        {
            _speech = new ScriptedSpeechSource();
            _speech.Load(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/Playback/speech.json")));
            _events = new List<string>();
            _speech.Partial += text => _events.Add("partial:" + text);
            _speech.Final += text => _events.Add("final:" + text);
        }
        [Test] public void File_ReplaysOnlyDuringHold_InOrder_Once()
        {
            Assert.IsTrue(_speech.Available);
            _speech.Tick(10);
            Assert.IsEmpty(_events);
            _speech.Begin();
            _speech.Tick(0.05f);
            _speech.Begin(); // Repeated PinchHold must not restart the answer.
            _speech.Tick(0.05f);
            _speech.Tick(1);
            CollectionAssert.AreEqual(new[] { "partial:private copper", "final:private copper lantern", "final:beside the window" }, _events);
            _speech.Tick(10);
            Assert.AreEqual(3, _events.Count);
        }
        [Test] public void Release_DropsFutureEvents_NextHoldUsesNextAnswer()
        {
            _speech.Begin();
            _speech.Tick(0.05f);
            _speech.End();
            _speech.End();
            _speech.Tick(20);
            Assert.IsFalse(_speech.Listening);
            _speech.Begin();
            _speech.Tick(1);
            CollectionAssert.AreEqual(new[] { "partial:private copper", "final:second answer" }, _events);
        }
        [Test] public void ExhaustedPlayback_DoesNotRepeatOrInventText()
        {
            for (int i = 0; i < 4; i++) { _speech.Begin(); _speech.Tick(1); _speech.End(); }
            Assert.AreEqual(4, _events.Count);
        }
        [TestCase("{\"utterances\":[{\"events\":[{\"t\":-1,\"kind\":\"final\",\"text\":\"x\"}]}]}")]
        [TestCase("{\"utterances\":[{\"events\":[{\"t\":0,\"kind\":\"unknown\",\"text\":\"x\"}]}]}")]
        [TestCase("{\"utterances\":[{\"events\":[{\"t\":2,\"kind\":\"final\",\"text\":\"x\"},{\"t\":1,\"kind\":\"final\",\"text\":\"y\"}]}]}")]
        [TestCase("{}")]
        public void InvalidLoad_PreservesPreviousPlayback(string json)
        {
            Assert.Throws<FormatException>(() => _speech.Load(json));
            _speech.Begin(); _speech.Tick(1);
            Assert.AreEqual(3, _events.Count);
        }
        [Test] public void InvalidTime_Refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _speech.Tick(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _speech.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _speech.Tick(float.PositiveInfinity));
        }
        [Test] public void Reload_StopsAndRewinds()
        {
            _speech.Begin(); _speech.Tick(1);
            _speech.Load("{\"utterances\":[]}");
            Assert.IsFalse(_speech.Listening);
            _speech.Begin(); _speech.Tick(1);
            Assert.AreEqual(3, _events.Count);
        }
    }
}
