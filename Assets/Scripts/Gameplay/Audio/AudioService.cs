using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ArcCreate.Gameplay.Audio
{
    public class AudioService : MonoBehaviour, IAudioService, IAudioControl
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private Slider timingSlider;

        /// <summary>
        /// Timing at which the audio started playing from.
        /// </summary>
        private int startTime = 0;

        /// <summary>
        /// Time (in dsp unit) at which the audio started playing.
        /// </summary>
        private double dspStartPlayingTime = 0;

        /// <summary>
        /// Dsp timing at the last frame.
        /// </summary>
        private double lastDspTiming = 0;

        /// <summary>
        /// Whether to let the timing stay unchanged until the audio start playing, or to increate it linearly.
        /// </summary>
        private bool stationaryBeforeStart;

        /// <summary>
        /// The current timing value in ms.
        /// </summary>
        private int audioTiming;

        /// <summary>
        /// Last timing the audio was paused at.
        /// </summary>
        private int lastPausedTiming = 0;

        /// <summary>
        /// Whether to return to <see cref="onPauseReturnTo"/> timing point after next pause.
        /// </summary>
        private bool returnOnPause = false;

        /// <summary>
        /// Return to this timing point after next pause.
        /// </summary>
        private int onPauseReturnTo = 0;
        private bool isStationary;
        private float updatePace = 1;

        public AudioSource AudioSource => audioSource;

        public VideoPlayer VideoPlayer => videoPlayer;

        public int ChartTiming
        {
            get => AudioTiming - FullOffset;
            set
            {
                AudioTiming = value + FullOffset;
            }
        }

        public int AudioTiming
        {
            get => audioTiming;
            set
            {
                audioTiming = value;
                timingSlider.value = Mathf.Clamp((float)value / AudioLength, 0, 1);
                if (IsPlaying)
                {
                    audioSource.Stop();
                    Play(audioTiming, 0);
                }

                Services.Chart.ResetJudge();
            }
        }

        public int AudioLength { get; private set; }

        public bool IsPlaying => audioSource.isPlaying;

        public bool IsPlayingAndNotStationary => (audioSource.isPlaying && !isStationary) || IsRendering;

        public bool IsLoaded => audioSource.clip != null;

        public bool IsRendering { get; set; }

        public AudioClip AudioClip
        {
            get => audioSource.clip;
            set
            {
                audioSource.clip = value;
                AudioLength = Mathf.RoundToInt(value.length * 1000);
            }
        }

        public AudioClip TapHitsoundClip => Services.Hitsound.TapHitsoundClip;

        public AudioClip ArcHitsoundClip => Services.Hitsound.ArcHitsoundClip;

        public Dictionary<string, AudioClip> SfxAudioClips => Services.Hitsound.SfxAudioClips;

        private int FullOffset => Values.ChartAudioOffset + Settings.GlobalAudioOffset.Value;

        public void SetAudioTimingSilent(int timing)
        {
            audioTiming = timing;
        }

        public void UpdateTime()
        {
            if (!IsPlaying)
            {
                return;
            }

            if (Application.isMobilePlatform || Settings.SyncToDSPTime.Value)
            {
                double dspTime = AudioSettings.dspTime;

                isStationary = stationaryBeforeStart && dspTime <= dspStartPlayingTime;

                if (stationaryBeforeStart)
                {
                    dspTime = Math.Max(dspTime, dspStartPlayingTime);
                }

                int timePassedSinceAudioStart = Mathf.RoundToInt((float)((dspTime - dspStartPlayingTime) * 1000));
                int newDspTiming = timePassedSinceAudioStart + startTime - FullOffset;
                int newTiming = audioTiming + Mathf.RoundToInt(Time.deltaTime * 1000 * updatePace);

                if (!stationaryBeforeStart || dspTime > dspStartPlayingTime)
                {
                    audioTiming = newTiming;
                }

                if (dspTime > dspStartPlayingTime && lastDspTiming != newDspTiming)
                {
                    float difference = (newDspTiming - newTiming) / 1000f;
                    if (difference >= 1 / 60f)
                    {
                        audioTiming = newDspTiming;
                    }

                    updatePace = 1 + (Tanh(difference) * 0.5f);
                }

                lastDspTiming = newDspTiming;
            }
            else
            {
                audioTiming = Mathf.RoundToInt(AudioSource.time * 1000f);
            }

            timingSlider.value = Mathf.Clamp((float)audioTiming / AudioLength, 0, 1);
        }

        public void PauseButtonPressed()
        {
            if (!IsPlaying)
            {
                ResumeImmediately();
            }
            else
            {
                Pause();
            }
        }

        public void Pause()
        {
            lastPausedTiming = audioTiming;
            audioSource.Stop();
            if (returnOnPause)
            {
                lastPausedTiming = onPauseReturnTo;
                AudioTiming = onPauseReturnTo;
            }

            SetEnableAutorotation(true);
        }

        public void Stop()
        {
            audioSource.Stop();
            lastPausedTiming = 0;
            AudioTiming = 0;

            SetEnableAutorotation(true);
        }

        public void PlayImmediately(int timing)
        {
            stationaryBeforeStart = false;
            returnOnPause = false;
            Play(timing, 0);
        }

        public void PlayWithDelay(int timing, int delayMs)
        {
            stationaryBeforeStart = false;
            returnOnPause = false;
            Play(timing, delayMs);
        }

        public void ResumeImmediately(bool resetJudge = true)
        {
            stationaryBeforeStart = true;
            returnOnPause = false;
            Play(lastPausedTiming, 0, resetJudge);
        }

        public void ResumeWithDelay(int delayMs, bool resetJudge = true)
        {
            stationaryBeforeStart = true;
            returnOnPause = false;
            Play(lastPausedTiming, delayMs, resetJudge);
        }

        public void ResumeReturnableImmediately()
        {
            stationaryBeforeStart = true;
            returnOnPause = true;
            onPauseReturnTo = audioTiming;
            Play(lastPausedTiming);
        }

        public void ResumeReturnableWithDelay(int delayMs)
        {
            stationaryBeforeStart = true;
            returnOnPause = true;
            onPauseReturnTo = audioTiming;
            Play(lastPausedTiming, delayMs);
        }

        public void SetResumeAt(int timing)
        {
            lastPausedTiming = timing;
        }

        public void SetReturnOnPause(bool cond, int timing = 0)
        {
            returnOnPause = cond;
            onPauseReturnTo = timing;
        }

        private void Play(int timing = 0, int delay = 0, bool resetJudge = true)
        {
            delay = Mathf.Max(delay, 0);
            if (timing >= AudioLength - 1)
            {
                timing = 0;
            }

            audioTiming = stationaryBeforeStart ? timing : timing - delay;
            updatePace = 1;

            if (resetJudge)
            {
                Services.Chart.ResetJudge();
            }

            audioSource.time = Mathf.Max(0, timing) / 1000f;
            if (timing < 0)
            {
                delay += -timing;
            }

            dspStartPlayingTime = AudioSettings.dspTime + ((double)delay / 1000);
            startTime = timing + FullOffset;
            if (delay > 0)
            {
                audioSource.PlayScheduled(dspStartPlayingTime);
            }
            else
            {
                audioSource.Play();
            }

            SetEnableAutorotation(false);
        }

        private void Awake()
        {
            gameplayData.AudioClip.OnValueChange += OnClipLoad;
            Settings.MusicAudio.OnValueChanged.AddListener(OnMusicAudioSettings);
            OnMusicAudioSettings(Settings.MusicAudio.Value);
        }

        private void OnDestroy()
        {
            gameplayData.AudioClip.OnValueChange -= OnClipLoad;
            Settings.MusicAudio.OnValueChanged.RemoveListener(OnMusicAudioSettings);
        }

        private void OnClipLoad(AudioClip clip)
        {
            AudioClip = clip;
        }

        private void SetEnableAutorotation(bool v)
        {
            Screen.autorotateToLandscapeLeft = v;
            Screen.autorotateToLandscapeRight = v;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        private void OnMusicAudioSettings(float volume)
        {
            audioSource.volume = Mathf.Clamp(volume, 0, 1);
        }

        private float Tanh(float v)
        {
            float exp2v = Mathf.Exp(v * 2);
            return (exp2v - 1) / (exp2v + 1);
        }
    }
}