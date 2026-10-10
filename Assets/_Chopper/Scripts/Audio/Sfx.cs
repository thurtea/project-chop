using UnityEngine;

namespace Chopper.Audio
{
    // Plays one shot sounds through a small pool of 3D audio sources, with pitch variation so repeated sounds
    // (footsteps, chops) do not sound identical. Created on first use and kept between scenes. Each source sits on its
    // own child object: they used to share one, so every new sound dragged the ones still playing to its position.
    public class Sfx : MonoBehaviour
    {
        private const int PoolSize = 24;
        private const float MaxDistance = 50f;
        private const float MinDistance = 2.5f;

        // Set from GameConfig.SfxVolume and the Effects slider by AudioDirector.
        public static float Master = 0.8f;

        private static Sfx _instance;
        private AudioSource[] _sources;
        private AudioSource _ui;
        private int _next;

        private static Sfx Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Sfx>();
                return _instance;
            }
        }

        private void Awake()
        {
            // Loud close by, falling off quickly at first and then gently, silent at the max distance: closer to how
            // sound carries outdoors than a straight line.
            var rolloff = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.1f, 0.62f), new Keyframe(0.3f, 0.3f), new Keyframe(0.6f, 0.1f), new Keyframe(1f, 0f));
            _sources = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
                source.minDistance = MinDistance;
                source.maxDistance = MaxDistance;
                source.dopplerLevel = 0f;
                source.spread = 40f;
                _sources[i] = source;
            }
            _ui = gameObject.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;
        }

        // pitchJitter is the +/- random spread around normal pitch, 0.1 is a subtle variation.
        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            if (clip == null) return;
            Instance.PlayInternal(clip, position, volume * SoundBank.GainOf(clip), pitch, pitchJitter);
        }

        // A flat (not positioned) sound for menus.
        public static void PlayUi(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            Instance._ui.PlayOneShot(clip, Mathf.Clamp01(volume * SoundBank.GainOf(clip) * Master));
        }

        private void PlayInternal(AudioClip clip, Vector3 position, float volume, float pitch, float pitchJitter)
        {
            // Take the next voice, but skip one that is still playing if a free one is close behind.
            AudioSource source = _sources[_next];
            for (int k = 0; k < 4 && source.isPlaying; k++)
            {
                _next = (_next + 1) % _sources.Length;
                source = _sources[_next];
            }
            _next = (_next + 1) % _sources.Length;
            source.Stop();
            source.transform.position = position;
            source.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume * Master);
            source.Play();
        }
    }
}
