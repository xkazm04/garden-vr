namespace GardenVR.Core
{
    public enum RitualPauseEvent { None, Resumed, ContinuePrompt }

    /// <summary>
    /// The pause-and-continue layer above <see cref="BreathSession"/> (PLAN gate S4). BreathSession keeps its own
    /// tracking pause; this adds the latched pause (palm open, focus loss, quit), the fresh-pinch rule, the 60 s
    /// "Continue?" prompt and the offer-back of a ritual cut off by quitting. Pure: the app reports what it sees.
    ///
    /// Per frame the caller: calls Tick(dt, pinching, sessionPaused) with <c>session.Phase == Paused</c> from the
    /// previous step; steps the session only while !Frozen, feeding a normal sample while !AwaitingContinue and either
    /// an untracked sample or nothing while AwaitingContinue; shows the prompt while AwaitingContinue; holds the view
    /// while Holding. Every output here is the same whether or not the session is stepped during the prompt: Holding
    /// is true throughout the prompt, and Continue decides from state the machine recorded when the prompt rose.
    /// </summary>
    public sealed class RitualPause
    {
        public const float ContinueAfterSeconds = 60f;

        bool _latched, _needFreshPinch, _sawOpen, _awaitContinue, _offered, _trackingOnly;
        float _pausedFor;

        /// <summary>Input is frozen: the session must not be stepped with a live sample.</summary>
        public bool Frozen => _latched || _needFreshPinch;
        /// <summary>The view holds still: frozen, waiting on Continue, or a tracking pause.</summary>
        public bool Holding { get; private set; }
        public bool AwaitingContinue => _awaitContinue;
        public float PausedFor => _pausedFor;

        /// <summary>Palm open, focus loss or quit. Idempotent while latched; under auto-pace a second palm resumes.</summary>
        public RitualPauseEvent Latch(bool autoPace)
        {
            if (_latched)
            {
                if (!autoPace || _awaitContinue) return RitualPauseEvent.None;
                Clear();
                return RitualPauseEvent.Resumed;
            }
            _latched = true;
            _needFreshPinch = !autoPace;
            _sawOpen = false;
            Holding = true;
            return RitualPauseEvent.None;
        }

        /// <summary>A ritual cut off by quitting is offered back, with the prompt up. It never starts itself.</summary>
        public void OfferBack(bool pinching)
        {
            _offered = _awaitContinue = _latched = _needFreshPinch = true;
            _trackingOnly = false;
            _sawOpen = !pinching;
            Holding = true;
        }

        /// <summary>A swapped hand source starts clean, except an offered ritual that is still waiting.</summary>
        public void SourceSwapped()
        {
            if (_offered && _awaitContinue) { _latched = _needFreshPinch = true; _sawOpen = false; return; }
            Clear();
            Holding = false;
        }

        /// <summary>
        /// The prompt was acknowledged. A latched pause (or a pending fresh-pinch) re-arms the fresh-pinch rule from
        /// the live hand: a pinch held now must be released first, so the acknowledging pinch never starts a breath.
        /// <paramref name="sessionPaused"/> is the caller's reading and is not consulted, so stepping the session
        /// during the prompt cannot change the answer; the reading at the moment the prompt rose is.
        /// </summary>
        public void Continue(bool sessionPaused, bool autoPace, bool pinching)
        {
            if (!_awaitContinue) return;
            bool wasLatched = !_trackingOnly;
            Clear();
            if (!autoPace && (wasLatched || pinching))
            {
                _needFreshPinch = true;
                _sawOpen = !pinching;
            }
            Holding = _needFreshPinch;
        }

        public RitualPauseEvent Tick(float dt, bool pinching, bool sessionPaused)
        {
            var ev = RitualPauseEvent.None;
            if (_needFreshPinch && !pinching) _sawOpen = true;
            if (_needFreshPinch && _sawOpen && !_awaitContinue && pinching)
            {
                Clear();
                ev = RitualPauseEvent.Resumed;
            }
            Holding = Frozen || _awaitContinue || sessionPaused;
            if (Holding)
            {
                _pausedFor += dt;
                if (_pausedFor > ContinueAfterSeconds && !_awaitContinue)
                {
                    _awaitContinue = true;
                    _trackingOnly = !Frozen && sessionPaused;
                    ev = RitualPauseEvent.ContinuePrompt;
                }
            }
            else _pausedFor = 0f;
            return ev;
        }

        void Clear()
        {
            _latched = _needFreshPinch = _sawOpen = _awaitContinue = _offered = _trackingOnly = false;
            _pausedFor = 0f;
        }
    }
}
