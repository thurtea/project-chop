using System.Collections.Generic;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.UI;
using Chopper.World;
using UnityEngine;

namespace Chopper.Audio
{
    // Client side music and the night horn. There is no background ambience on purpose (no birds, crickets, wind or
    // surf): the world is heard through its effects, and later through weather. The music follows the place and the
    // hour (MusicRules): the hub's cozy day music, each forest's own, the calm night, and the fight while awake enemies
    // are close. Every mood is its own playlist that plays one track after another, and the moods crossfade. A horn
    // sounds when night falls.
    public class AudioDirector : MonoBehaviour
    {
        private const float MusicTrim = 0.45f;
        // Music fades down inside a crypt, where the effects and the dark carry the mood.
        private const float CryptMusicScale = 0.35f;
        private const float FadeSpeed = 0.35f;
        private const float StingerVolume = 0.7f;
        private const float FightCheckSeconds = 0.5f;
        // The fight music stays this long after the last enemy is gone, so it does not flicker.
        private const float FightHoldSeconds = 6f;

        [SerializeField] private GameConfig _config;

        private sealed class Playlist
        {
            public AudioSource Source;
            public AudioClip[] Tracks;
            public int Index;
            public bool Paused;
            public bool Started;
        }

        private readonly Dictionary<MusicMood, Playlist> _playlists = new Dictionary<MusicMood, Playlist>();
        private DayPhase _lastPhase = DayPhase.Day;
        private bool _hasPhase;
        private float _nextFightCheck;
        private float _fightUntil = float.NegativeInfinity;

        private void Awake()
        {
            for (int i = 0; i < MusicRules.Count; i++)
            {
                var mood = (MusicMood)i;
                AudioClip[] tracks = Tracks(mood);
                if (tracks.Length == 0) continue;
                var source = gameObject.AddComponent<AudioSource>();
                source.clip = tracks[0];
                source.loop = tracks.Length == 1;
                source.spatialBlend = 0f;
                source.volume = 0f;
                source.playOnAwake = false;
                _playlists[mood] = new Playlist { Source = source, Tracks = tracks };
            }
        }

        // The mood's own files, or the AlkaKrab tracks it falls back on.
        private static AudioClip[] Tracks(MusicMood mood)
        {
            AudioClip[] own = SoundBank.All(MusicRules.Name(mood));
            if (own.Length > 0) return own;
            var list = new List<AudioClip>();
            foreach (string name in MusicRules.Fallback(mood)) list.AddRange(SoundBank.All(name));
            return list.ToArray();
        }

        private void Update()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null) return;

            // The sliders are read every frame so a change is heard at once.
            Sfx.Master = (_config != null ? _config.SfxVolume : 1f) * UserSettings.EffectsVolume;
            float music = UserSettings.MusicVolume * MusicTrim * Mathf.Lerp(1f, CryptMusicScale, DayNightCycle.UndergroundBlend);
            Element zone = ZoneTracker.Current;
            bool fight = FightNear();
            foreach (KeyValuePair<MusicMood, Playlist> pair in _playlists)
            {
                Playlist list = pair.Value;
                float target = MusicRules.Weight(pair.Key, zone, cycle.Darkness, fight) * music;
                list.Source.volume = Mathf.MoveTowards(list.Source.volume, target, FadeSpeed * music * Time.deltaTime + 0.0001f);
                // Silent playlists pause; a mood coming back picks up where it left off.
                bool wanted = target > 0f || list.Source.volume > 0.0001f;
                if (wanted && !list.Source.isPlaying) NextOrResume(list);
                else if (!wanted && list.Source.isPlaying)
                {
                    list.Source.Pause();
                    list.Paused = true;
                }
            }

            if (!_hasPhase)
            {
                _lastPhase = cycle.Phase;
                _hasPhase = true;
                return;
            }
            if (cycle.Phase == _lastPhase) return;

            _lastPhase = cycle.Phase;
            if (cycle.Phase == DayPhase.Night) Sfx.PlayUi(ProceduralSfx.Horn, StingerVolume);
        }

        // A paused track carries on; a finished one gives way to the next in the playlist.
        private static void NextOrResume(Playlist list)
        {
            AudioSource source = list.Source;
            if (list.Paused)
            {
                list.Paused = false;
                source.UnPause();
                return;
            }
            if (list.Started && !source.loop)
            {
                list.Index = (list.Index + 1) % list.Tracks.Length;
                source.clip = list.Tracks[list.Index];
            }
            list.Started = true;
            source.Play();
        }

        // True while an awake enemy is close to the local player (checked twice a second, held a few seconds after).
        private bool FightNear()
        {
            if (Time.time >= _nextFightCheck)
            {
                _nextFightCheck = Time.time + FightCheckSeconds;
                PlayerWood local = PlayerWood.Local;
                if (local != null)
                {
                    Vector3 here = local.transform.position;
                    foreach (Treant treant in Treant.ClientAll)
                    {
                        if (treant == null || !treant.IsActive) continue;
                        if ((treant.transform.position - here).sqrMagnitude > MusicRules.FightRange * MusicRules.FightRange) continue;
                        _fightUntil = Time.time + FightHoldSeconds;
                        break;
                    }
                }
            }
            return Time.time < _fightUntil;
        }
    }
}
