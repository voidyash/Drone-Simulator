using UnityEngine;

namespace DroneSimulator.Audio
{
    /// <summary>
    /// Persistent background-music manager. One instance survives scene loads
    /// (duplicates destroy themselves), so the same BGM keeps playing from
    /// the Start Menu into the Simulator without restarting.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioClip bgmClip;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.15f;
        [SerializeField] private bool playOnAwake = true;

        public static AudioManager Instance { get; private set; }

        private AudioSource bgmSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
            }

            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;

            if (playOnAwake)
            {
                PlayBGM();
            }
        }

        public void PlayBGM()
        {
            PlayBGM(bgmClip);
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null)
            {
                return;
            }

            // Idempotent: never restart the same music (scene transitions).
            if (bgmSource.clip == clip && bgmSource.isPlaying)
            {
                return;
            }

            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }
    }
}
