using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PhotonVoiceRecorder))]
public class PhotonVoiceRecorderInspector : Editor
{
    public override void OnInspectorGUI()
    {
        PhotonVoiceRecorder rec = target as PhotonVoiceRecorder;
        rec.Source = (PhotonVoiceRecorder.AudioSource)EditorGUILayout.EnumPopup("Source", rec.Source);
        rec.TypeConvert =
            (PhotonVoiceRecorder.SampleTypeConv) EditorGUILayout.EnumPopup("Type Convert", rec.TypeConvert); 
        switch (rec.Source)
        {
            case PhotonVoiceRecorder.AudioSource.Microphone:
                rec.MicrophoneType =
                    (PhotonVoiceRecorder.MicAudioSourceType) EditorGUILayout.EnumPopup("Microphone Type",
                        rec.MicrophoneType);
                break;
            case PhotonVoiceRecorder.AudioSource.AudioClip:
                rec.AudioClip = EditorGUILayout.ObjectField("Audio Clip", rec.AudioClip, typeof(AudioClip), true) as AudioClip;
                rec.LoopAudioClip = EditorGUILayout.Toggle("Loop Audio Clip", rec.LoopAudioClip);
                break;
            case PhotonVoiceRecorder.AudioSource.Factory:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
