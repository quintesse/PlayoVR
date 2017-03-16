using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour {

    public AudioClip[] sounds;

    private Dictionary<string, int> nameToId = new Dictionary<string, int>();
    private Dictionary<int, AudioClip> idToClip = new Dictionary<int, AudioClip>();

    private static AudioManager manager;

    void Awake() {
        manager = this;
        int id = 1;
		foreach (AudioClip sound in sounds) {
            nameToId[sound.name] = id;
            idToClip[id++] = sound;
        }
	}

    public static int GetClipId(AudioClip clip) {
        return GetClipId(clip.name);
    }

    public static int GetClipId(string clipName) {
        if (manager != null && manager.nameToId.ContainsKey(clipName)) {
            return manager.nameToId[clipName];
        } else {
            return -1;
        }
    }

    public static AudioClip GetClip(int id) {
        if (manager != null) {
            return manager.idToClip[id];
        } else {
            return null;
        }
    }

}
