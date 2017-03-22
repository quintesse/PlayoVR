using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour {

    public AudioClip[] sounds;

    private Dictionary<string, int> nameToId = new Dictionary<string, int>();
    private Dictionary<int, AudioClip> idToClip = new Dictionary<int, AudioClip>();

    private static AudioManager instance;
    private static object instanceLock = new object();

    void Awake() {
        lock (instanceLock) {
            if (instance == null) {
                instance = this;
            } else {
                Debug.LogError("Error: Attempt to create multiple instances of AudioManager");
            }
        }

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
        if (instance != null && instance.nameToId.ContainsKey(clipName)) {
            return instance.nameToId[clipName];
        } else {
            return -1;
        }
    }

    public static AudioClip GetClip(int id) {
        if (instance != null) {
            return instance.idToClip[id];
        } else {
            return null;
        }
    }

}
