using System;
using UnityEngine;

namespace AnomalyArena
{
    [Serializable]
    public sealed class ArenaAudioClip
    {
        public ArenaSoundCue cue;
        public AudioClip clip;
        [Tooltip("当前使用已有音频临时代替；正式音效文件到位后安装器会迁移此引用。")] public bool temporaryPlaceholder;
        [Range(0f, 1f), Tooltip("此类短音的增益；原始音频文件不会被修改。")] public float gain = .6f;
        [Range(.25f, 3f), Tooltip("此类短音的基础音调。")] public float pitch = 1f;
        [Tooltip("同类事件两次发声的最小间隔，单位秒。")] public float minInterval = .08f;
        [Range(1, 8), Tooltip("同类事件可同时占用的保存声道数。")] public int maxConcurrent = 2;
        [Tooltip("播放窗口，0表示完整片段。旧声音临时映射用短窗口，保留MP3原件。")] public float playbackSeconds;
    }
}
