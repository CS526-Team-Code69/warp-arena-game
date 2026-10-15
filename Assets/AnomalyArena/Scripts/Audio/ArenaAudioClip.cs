using System;
using UnityEngine;

namespace AnomalyArena
{
    [Serializable]
    public sealed class ArenaAudioClip
    {
        public ArenaSoundCue cue;
        public AudioClip clip;
        [Tooltip("Temporary reference to an approved existing clip; replaced when the dedicated ElevenLabs file is installed.")] public bool temporaryPlaceholder;
        [Range(0f, 1f), Tooltip("Final gain after normalization; source audio is not modified.")] public float gain = .6f;
        [Range(.25f, 3f), Tooltip("Display remaining offscreen enemies.")] public float pitch = 1f;
        [Tooltip("Minimum interval between sounds of this event type, in seconds.")] public float minInterval = .08f;
        [Range(1, 8), Tooltip("Maximum saved voices used simultaneously by this event type.")] public int maxConcurrent = 2;
        [Tooltip("Playback window; zero plays the whole clip. Does not modify the source MP3.")] public float playbackSeconds;
    }
}
