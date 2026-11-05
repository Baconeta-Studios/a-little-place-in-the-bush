using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class CustomAudioSource : MonoBehaviour
    {
        private AudioSource _self;
        private Coroutine _resetCoroutine;
        private Coroutine _fadeCoroutine;

        /// <summary>The looping clip it's playing, if any.</summary>
        public AudioClip LoopingClip => _self ? _self.clip : null;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void ResetData()
        {
            if (_self != null)
            {
                _self.Stop();
            }
            _resetCoroutine = null;
            Destroy(gameObject);
        }

        public void Init(AudioMixerGroup group)
        {
            if (!_self) _self = GetComponent<AudioSource>();
            _self.outputAudioMixerGroup = group;
        }

        public void PlayOnce(AudioClip clip, float volume)
        {
            if (!_self) _self = GetComponent<AudioSource>();

            // Cancel any pending destroy so this clip gets its full duration.
            if (_resetCoroutine != null)
            {
                StopCoroutine(_resetCoroutine);
            }

            _self.PlayOneShot(clip, volume);
            _resetCoroutine = StartCoroutine(ResetObject(clip.length / Mathf.Max(0.1f, _self.pitch) + 0.5f));
        }

        public void PlayLooping(AudioClip clip, float volume)
        {
            if (!_self) _self = GetComponent<AudioSource>();
            _self.clip = clip;
            _self.loop = true;
            _self.volume = volume;
            _self.Play();
        }

        /// <summary>Above 1 is higher and quicker. Set it before playing.</summary>
        public void SetPitch(float pitch)
        {
            if (!_self) _self = GetComponent<AudioSource>();
            _self.pitch = pitch;
        }

        /// <summary>Fades to a volume, in real time, so it still works while the game is paused.</summary>
        public void FadeTo(float volume, float seconds, bool stopAfter = false)
        {
            if (!_self) _self = GetComponent<AudioSource>();
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(Fade(volume, seconds, stopAfter));
        }

        private IEnumerator Fade(float volume, float seconds, bool stopAfter)
        {
            float from = _self.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _self.volume = Mathf.Lerp(from, volume, t / seconds);
                yield return null;
            }
            _self.volume = volume;
            _fadeCoroutine = null;
            if (stopAfter) StopAudio();
        }

        public void StopAudio()
        {
            if (_resetCoroutine != null)
            {
                StopCoroutine(_resetCoroutine);
            }
            _resetCoroutine = StartCoroutine(ResetObject(0f));
        }

        private IEnumerator ResetObject(float delay)
        {
            yield return new WaitForSecondsRealtime(delay); // the game pauses behind most screens
            ResetData();
        }
    }
}
