using UnityEngine;
using System.Collections;
using System;
#if !UNITY_EDITOR && UNITY_PS4
    using Sony.NP;
#endif

public class Gui : MonoBehaviour {

	// Use this for initialization
	void Start () {
        PhotonNetwork.networkingPeer.TrafficStatsEnabled = true;
		PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsEnabled = true;

        // Setting factory below replicates AudioSource.DeviceMicrophone behavior.
        PhotonVoiceNetwork.AudioSourceFactory = (rec) => new MicWrapper(rec.MicrophoneDevice != null ? rec.MicrophoneDevice : PhotonVoiceNetwork.MicrophoneDevice, (int)PhotonVoiceSettings.Instance.SamplingRate);
#if UNITY_IOS && !UNITY_EDITOR
        PhotonVoiceNetwork.AudioSourceFactory = (rec) => new AppleAudioInPusher(-1);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        PhotonVoiceNetwork.AudioSourceFactory = (rec) => new AndroidAudioInAEC();       
#endif

        //PhotonVoiceNetwork.Client.loadBalancingPeer.DebugOut = ExitGames.Client.Photon.DebugLevel.ALL;
        //PhotonNetwork.logLevel = PhotonLogLevel.Full;
    }

    float dataRateNextTime = 0;
    int prevInBytes;
    int prevOutBytes;
    int dataRateIn;
    int dataRateOut;
	// Update is called once per frame
	void OnGUI()
    {
        // TODO: better way to ref recorder
        PhotonVoiceRecorder rec = null;
        foreach (var r in FindObjectsOfType<PhotonVoiceRecorder>())
        {
            if (r.photonView.isMine)
            {
                rec = r;
                break;
            }
        }
        
        var lStyle = new GUIStyle("label");
        lStyle.fontSize = 24 * Screen.height / 600;
        lStyle.wordWrap = false;
        var lStyleSmall = new GUIStyle("label");
        lStyleSmall.fontSize = 16 * Screen.height / 600;
        lStyleSmall.wordWrap = false;
        var bStyle = new GUIStyle("button");
        bStyle.fontSize = 28 * Screen.height / 600;
        var bStyleSmall = new GUIStyle("button");
        bStyleSmall.fontSize = 16 * Screen.height / 600;

        var roomName = "";
        if (PhotonNetwork.inRoom)
        {
            roomName = PhotonNetwork.room.Name;
        }
        string rttString = String.Format(
            "RTT/Var/Que: {0}/{1}/{2}",
            PhotonNetwork.networkingPeer.RoundTripTime.ToString(),
            PhotonNetwork.networkingPeer.RoundTripTimeVariance,
            PhotonNetwork.networkingPeer.QueuedIncomingCommands);
        GUILayout.Label("PUN: " + PhotonNetwork.connectionStateDetailed.ToString() + " " + roomName + " " + rttString, lStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect", bStyle))
        {
            PhotonNetwork.ConnectUsingSettings(string.Format("1.{0}", SceneManagerHelper.ActiveSceneBuildIndex));
        }
        if (GUILayout.Button("Disconnect", bStyle))
        {
            PhotonNetwork.Disconnect();
        }
        GUILayout.EndHorizontal();
        roomName = "";
        if (PhotonVoiceNetwork.ClientState == ExitGames.Client.Photon.LoadBalancing.ClientState.Joined) 
        {
            roomName = PhotonVoiceNetwork.CurrentRoomName;
        }

        if (dataRateNextTime < Time.time)
        {
            dataRateNextTime = Time.time + 1;
            dataRateIn = (PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsIncoming.TotalPacketBytes - prevInBytes)/1;
            dataRateOut = (PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsOutgoing.TotalPacketBytes - prevOutBytes)/1;
            prevInBytes = PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsIncoming.TotalPacketBytes;
            prevOutBytes = PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsOutgoing.TotalPacketBytes;
        }

        rttString = String.Format(
            "RTT/Var/Que: {0}/{1}/{2}",
            PhotonVoiceNetwork.Client.loadBalancingPeer.RoundTripTime.ToString(),
            PhotonVoiceNetwork.Client.loadBalancingPeer.RoundTripTimeVariance,
            PhotonVoiceNetwork.Client.loadBalancingPeer.QueuedIncomingCommands);
        GUILayout.Label("PhotonVoice: " + PhotonVoiceNetwork.ClientState.ToString() + " " + roomName + " " + rttString, lStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Data rate in/out bytes/sec: " + dataRateIn + "/" + dataRateOut, lStyleSmall);
        if (PhotonVoiceNetwork.Client.loadBalancingPeer != null)
        {
            GUILayout.Label("Traffic bytes: " + PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsIncoming.TotalPacketBytes + "/" + PhotonVoiceNetwork.Client.loadBalancingPeer.TrafficStatsOutgoing.TotalPacketBytes, lStyleSmall);
        }
        GUILayout.Label("Frames Sent/Rcvd/Lost: " + PhotonVoiceNetwork.VoiceClient.FramesSent + "/" + PhotonVoiceNetwork.VoiceClient.FramesReceived + "/" + PhotonVoiceNetwork.VoiceClient.FramesLost, lStyleSmall);
        GUILayout.Label("Voice RTT/Var: " + PhotonVoiceNetwork.VoiceClient.RoundTripTime + "/" + PhotonVoiceNetwork.VoiceClient.RoundTripTimeVariance, lStyleSmall);
        
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();

        GUILayout.Label("Speakers:", lStyleSmall);
        foreach (var s in FindObjectsOfType<PhotonVoiceSpeaker>())
        {
            if (s.IsVoiceLinked)
            {
                GUILayout.Label("lag=" + s.CurrentBufferLag, lStyleSmall);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect", bStyle))
        {
            PhotonVoiceNetwork.Connect();
        }
        if (GUILayout.Button("Disconnect", bStyle))
        {
            PhotonVoiceNetwork.Disconnect();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();

        if (rec != null && rec.photonView.isMine)
        {
            if (GUILayout.Button((rec.DebugEchoMode ? "[X] " : "[ ] ") + "Debug Echo", bStyle))
            {
                rec.DebugEchoMode = !rec.DebugEchoMode;
            }

            if (GUILayout.Button((rec.Transmit ? "[X] ": "[ ] ") + "Transmit", bStyle))
            {
                rec.Transmit = !rec.Transmit;
            }
            if (GUILayout.Button((rec.VoiceDetector.On ? "[X] " : "[ ] ") + "Detect", bStyle))
            {
                rec.Detect = !rec.Detect;
            }            
            if (GUILayout.Button((rec.VoiceDetectorCalibrating ? "[X] " : "[ ] ") + "Calibrate Detector", bStyle))
            {
                rec.VoiceDetectorCalibrate(2000);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Transmitting: " + rec.IsTransmitting, lStyleSmall);
            GUILayout.Label("Avg Amp: " + (rec.LevelMeter == null ? "" : rec.LevelMeter.CurrentAvgAmp.ToString("0.000000") + "/" + rec.LevelMeter.AccumAvgPeakAmp.ToString("0.000000")), lStyleSmall);
            GUILayout.Label("Peak Amp: " + (rec.LevelMeter == null ? "" : rec.LevelMeter.CurrentPeakAmp.ToString("0.000000")), lStyleSmall);
            GUILayout.Label("Detector Threshold: " + (rec.VoiceDetector == null ? "" : rec.VoiceDetector.Threshold.ToString("0.000000")), lStyleSmall);
            GUILayout.Label("Audio group (rec): " + rec.AudioGroup.ToString(), lStyleSmall);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var speexDSP = rec.GetComponent<SpeexDSP>();
            if (speexDSP != null)
            {
                if (GUILayout.Button((speexDSP.AEC ? "[X] AEC" : "[ ] AEC"), bStyle))
                {
                    speexDSP.AEC = !speexDSP.AEC;
                }

                if (GUILayout.Button("Reset", bStyle))
                {
                    speexDSP.ResetAEC();
                }

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                GUILayout.Label("delay:", lStyle);
                var x = speexDSP.AECPlaybackDelayMs.ToString();
                var tStyle = new GUIStyle("textfield");
                tStyle.fontSize = bStyle.fontSize;
                speexDSP.AECPlaybackDelayMs = Int32.Parse(GUILayout.TextField(x, tStyle));
                GUILayout.EndHorizontal();
                
                if (GUILayout.Button((speexDSP.AECLatencyDetect ? "[X] Latency Detect:" : "[ ] Latency Detect"), bStyle))
                {
                    speexDSP.AECLatencyDetect = !speexDSP.AECLatencyDetect;
                }
                if (speexDSP.AECLatencyDetect)
                {
                    var l = speexDSP.Processor.AECLatencyResult;
                    GUILayout.Label(l.LatencyMs + "/" + l.LatencyDelayedMs + " ms", lStyle);
                    GUILayout.Label(
                        (l.PlayDetected ? "!" : ".") +
                        (l.PlayDelayedDetected ? "!" : ".") +
                        (l.RecDetected ? "!" : "."),
                        lStyle);
                }                                
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Set Group (offs Debug Echo): ", lStyleSmall);
        for (byte i = 0; i < 5; i++)
        {
            if (GUILayout.Button((PhotonVoiceNetwork.Client.GlobalAudioGroup == i ? "[X] " : "[ ] ")  + (i == 0 ? "No" : i.ToString()), bStyleSmall))
            {
                PhotonVoiceNetwork.Client.GlobalAudioGroup = i;
            }
        }
        GUILayout.EndHorizontal();

        var pvs = PhotonVoiceSettings.Instance;
        GUILayout.BeginHorizontal();
		GUILayout.Label("Unity Mic: ", lStyleSmall);
		foreach (var x in Microphone.devices) 
		{            
			if (GUILayout.Button ((pvs.MicrophoneType == PhotonVoiceSettings.MicAudioSourceType.Unity && PhotonVoiceNetwork.MicrophoneDevice == x ? "[X] " : "[ ] ") + x, bStyleSmall)) 
			{
                pvs.MicrophoneType = PhotonVoiceSettings.MicAudioSourceType.Unity;
                PhotonVoiceNetwork.MicrophoneDevice = x;
			}
		}
        GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label("Photon Mic: ", lStyleSmall);

        if (PhotonVoiceNetwork.PhotonMicrophoneEnumerator.IsSupported) 
		{
			for (int i = 0; i < PhotonVoiceNetwork.PhotonMicrophoneEnumerator.Count; i++) 
			{
				if (GUILayout.Button ((pvs.MicrophoneType == PhotonVoiceSettings.MicAudioSourceType.Photon && PhotonVoiceNetwork.PhotonMicrophoneDeviceID == PhotonVoiceNetwork.PhotonMicrophoneEnumerator.IDAtIndex(i) ? "[X] " : "[ ] ") + PhotonVoiceNetwork.PhotonMicrophoneEnumerator.NameAtIndex(i), bStyleSmall)) {
					pvs.MicrophoneType = PhotonVoiceSettings.MicAudioSourceType.Photon;
					PhotonVoiceNetwork.PhotonMicrophoneDeviceID = PhotonVoiceNetwork.PhotonMicrophoneEnumerator.IDAtIndex(i);
				}
			}
            if (GUILayout.Button("Refresh", bStyleSmall))
            {
                PhotonVoiceNetwork.PhotonMicrophoneEnumerator.Refresh();
            }

        } 
		else 
		{
			// use 1st device in Unity device enumeration to display default device name (this may be wrong of course)
			if (Microphone.devices.Length > 0)
			{
				if (GUILayout.Button ((pvs.MicrophoneType == PhotonVoiceSettings.MicAudioSourceType.Photon ? "[X] " : "[ ] ") + Microphone.devices[0], bStyleSmall)) {
					pvs.MicrophoneType = PhotonVoiceSettings.MicAudioSourceType.Photon;
					PhotonVoiceNetwork.PhotonMicrophoneDeviceID = -1;
				}
			}
		}
		GUILayout.EndHorizontal();
        GUI.enabled = true;

#if !UNITY_EDITOR && UNITY_PS4
        UserProfiles.LocalUsers localUsers = new UserProfiles.LocalUsers();
        UserProfiles.GetLocalUsers(localUsers);
        int userID = localUsers.LocalUsersIds[0].UserId.Id;

        pvs.PS4UserID = userID;
#endif
    }
}
