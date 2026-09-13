using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Persistent sound player: pooled one-shots with pitch variation, layered music, ambient loop.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] int sfxVoices = 16;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;
        [SerializeField] float layerFadeSeconds = 1.5f;

        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly List<AudioSource> musicLayers = new List<AudioSource>();
        readonly List<float> layerTargets = new List<float>();
        AudioSource ambient;
        float ambientTarget;
        int nextVoice;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < sfxVoices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                voices.Add(src);
            }
            ambient = gameObject.AddComponent<AudioSource>();
            ambient.loop = true;
            ambient.playOnAwake = false;
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariance = 0.1f)
        {
            if (clip == null) return;
            AudioSource src = FreeVoice();
            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.volume = volume * sfxVolume;
            src.clip = clip;
            src.Play();
        }

        // Starts every layer in sync; SetMusicLayers fades them in and out.
        public void PlayMusic(AudioClip[] layers)
        {
            if (layers == null || layers.Length == 0) return;
            if (musicLayers.Count == layers.Length && musicLayers[0].clip == layers[0] && musicLayers[0].isPlaying) return;

            foreach (var src in musicLayers) Destroy(src);
            musicLayers.Clear();
            layerTargets.Clear();

            double startAt = AudioSettings.dspTime + 0.1;
            foreach (var clip in layers)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.volume = 0f;
                if (clip != null) src.PlayScheduled(startAt);
                musicLayers.Add(src);
                layerTargets.Add(0f);
            }
        }

        public void SetMusicLayers(int count)
        {
            for (int i = 0; i < layerTargets.Count; i++) layerTargets[i] = i < count ? 1f : 0f;
        }

        public void StopMusic(bool instant)
        {
            for (int i = 0; i < musicLayers.Count; i++)
            {
                layerTargets[i] = 0f;
                if (instant) musicLayers[i].volume = 0f;
            }
            ambientTarget = 0f;
            if (instant) ambient.volume = 0f;
        }

        public void PlayAmbient(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (ambient.clip != clip)
            {
                ambient.clip = clip;
                ambient.volume = 0f;
                ambient.Play();
            }
            ambientTarget = volume;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, layerFadeSeconds);
            for (int i = 0; i < musicLayers.Count; i++)
                musicLayers[i].volume = Mathf.MoveTowards(musicLayers[i].volume, layerTargets[i] * musicVolume, step * musicVolume);
            ambient.volume = Mathf.MoveTowards(ambient.volume, ambientTarget, step);
        }

        AudioSource FreeVoice()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                AudioSource src = voices[(nextVoice + i) % voices.Count];
                if (!src.isPlaying)
                {
                    nextVoice = (nextVoice + i + 1) % voices.Count;
                    return src;
                }
            }
            AudioSource oldest = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            return oldest;
        }
    }
}
