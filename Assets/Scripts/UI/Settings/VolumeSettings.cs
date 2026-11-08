using System;
using Managers;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.Settings
{
    public class VolumeSettings : MonoBehaviour
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField, FormerlySerializedAs("ambientSlider")] private Slider dialogueSlider;

        public const string MixerMusic = "MusicVolume";
        public const string SfxMusic = "SFXVolume";
        public const string MixerDialogue = "DialogueVolume";

        private void Awake()
        {
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
            dialogueSlider.onValueChanged.AddListener(SetDialogueVolume);
        }

        private void Start()
        {
            musicSlider.value = PlayerPrefs.GetFloat(AudioManager.MusicKey, 0.5f);
            sfxSlider.value = PlayerPrefs.GetFloat(AudioManager.SfxKey, 0.5f);
            dialogueSlider.value = PlayerPrefs.GetFloat(AudioManager.DialogueKey, 0.5f);
        }

        private void SetMusicVolume(float value)
        {
            mixer.SetFloat(MixerMusic, Mathf.Log10(value) * 20);
        }

        private void SetSfxVolume(float value)
        {
            mixer.SetFloat(SfxMusic, Mathf.Log10(value) * 20);
        }

        private void SetDialogueVolume(float value)
        {
            mixer.SetFloat(MixerDialogue, Mathf.Log10(value) * 20);
        }

        private void OnDisable()
        {
            PlayerPrefs.SetFloat(AudioManager.MusicKey, musicSlider.value);
            PlayerPrefs.SetFloat(AudioManager.SfxKey, sfxSlider.value);
            PlayerPrefs.SetFloat(AudioManager.DialogueKey, dialogueSlider.value);
            // WebGL only persists prefs on Save(); closing the tab skips the quit-time flush.
            PlayerPrefs.Save();
        }
    }
}