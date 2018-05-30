// ----------------------------------------------------------------------------
// <copyright file="PhotonVoiceHandler.cs" company="Exit Games GmbH">
// Photon Voice for PUN - Copyright (C) Exit Games GmbH
// </copyright>
// <summary>
// Class that keeps connection alive while app is on the background
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

using System.Diagnostics;
using UnityEngine;
using SupportClassPun = ExitGames.Client.Photon.SupportClass;

#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

[DisallowMultipleComponent]
public class PhotonVoiceHandler : MonoBehaviour
{
    public int updateInterval;  // time [ms] between consecutive SendOutgoingCommands calls

    private int nextSendTickCount;

    private static bool sendThreadShouldRun;

    private static Stopwatch timerToStopConnectionInBackground;

    private static void StartFallbackSendAckThread()
    {
#if !UNITY_WEBGL
        if (sendThreadShouldRun)
        {
            return;
        }

        sendThreadShouldRun = true;
        SupportClassPun.StartBackgroundCalls(FallbackSendAckThread);   // thread will call this every 100ms until method returns false
#endif
    }

    private static void StopFallbackSendAckThread()
    {
#if !UNITY_WEBGL
        sendThreadShouldRun = false;
#endif
    }

    /// <summary>A thread which runs independent from the Update() calls. Keeps connections online while loading or in background. See PhotonVoiceNetwork.BackgroundTimeout.</summary>
    private static bool FallbackSendAckThread()
    {
        if (sendThreadShouldRun && PhotonVoiceNetwork.Client != null && PhotonVoiceNetwork.Client.loadBalancingPeer != null)
        {
            ExitGames.Client.Photon.LoadBalancing.LoadBalancingPeer voicePeer = PhotonVoiceNetwork.Client.loadBalancingPeer;
            ExitGames.Client.Photon.LoadBalancing.ClientState clientState = PhotonVoiceNetwork.Client.State;
            // check if the client should disconnect after some seconds in background
            if (timerToStopConnectionInBackground != null && PhotonVoiceNetwork.BackgroundTimeout > 0.1f)
            {
                if (timerToStopConnectionInBackground.ElapsedMilliseconds > PhotonVoiceNetwork.BackgroundTimeout * 1000)
                {
                    bool connected = true;
                    switch (clientState)
                    {
                        case ExitGames.Client.Photon.LoadBalancing.ClientState.PeerCreated:
                        case ExitGames.Client.Photon.LoadBalancing.ClientState.Disconnected:
                        case ExitGames.Client.Photon.LoadBalancing.ClientState.Disconnecting:
                        case ExitGames.Client.Photon.LoadBalancing.ClientState.ConnectedToNameServer:
                            connected = false;
                            break;
                    }
                    if (connected)
                    {
                        PhotonVoiceNetwork.Disconnect();
                    }
                    timerToStopConnectionInBackground.Stop();
                    timerToStopConnectionInBackground.Reset();
                    return sendThreadShouldRun;
                }
            }

            if (voicePeer.ConnectionTime - voicePeer.LastSendOutgoingTime > 200)
            {
                voicePeer.SendAcksOnly();
            }
        }

        return sendThreadShouldRun;
    }

    [RuntimeInitializeOnLoadMethod]
    private static void RuntimeInitializeOnLoad()
    {
        // Set up a MonoBehaviour to run Photon Voice, and hide it
        string HiddenVoiceGameObjectName = "PhotonVoiceMono";
        GameObject photonGO = GameObject.Find(HiddenVoiceGameObjectName);
        if (photonGO == null)
        {
            photonGO = new GameObject();
            photonGO.name = HiddenVoiceGameObjectName;
        }
        if (photonGO.GetComponent<PhotonVoiceHandler>() == null)
        {
            photonGO.AddComponent<PhotonVoiceHandler>();
        }
        photonGO.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(photonGO);
    }

    private void Awake()
    {
        // make sure that instance exists before starting thread (instance can be created in main thread only)
        if (null != PhotonVoiceNetwork.instance)
        {
            StartFallbackSendAckThread();
        }
        else
        {
            UnityEngine.Debug.LogError("[PUNVoice]: \"FallbackSendAckThread\" not started because PhotonVoiceNetwork instance not ready yet.");
        }
    }

    protected void Update()
    {
        ExitGames.Client.Photon.LoadBalancing.LoadBalancingPeer voicePeer = PhotonVoiceNetwork.Client.loadBalancingPeer;
        if (voicePeer == null)
        {
            UnityEngine.Debug.LogError("[PUNVoice]: LoadBalancingPeer broke!");
            return;
        }

        ExitGames.Client.Photon.LoadBalancing.ClientState clientState = PhotonVoiceNetwork.Client.State;
        bool connected = true;
        switch (clientState)
        {
            case ExitGames.Client.Photon.LoadBalancing.ClientState.PeerCreated:
            case ExitGames.Client.Photon.LoadBalancing.ClientState.Disconnected:
            case ExitGames.Client.Photon.LoadBalancing.ClientState.Disconnecting:
            case ExitGames.Client.Photon.LoadBalancing.ClientState.ConnectedToNameServer:
                connected = false;
                break;
        }
        if (!connected)
        {
            return;
        }

        bool doDispatch = true;
        while (PhotonNetwork.isMessageQueueRunning && doDispatch)
        {
            // DispatchIncomingCommands() returns true of it found any command to dispatch (event, result or state change)
            Profiler.BeginSample("[PUNVoice]: DispatchIncomingCommands");
            doDispatch = voicePeer.DispatchIncomingCommands();
            Profiler.EndSample();
        }

        int currentMsSinceStart = (int)(Time.realtimeSinceStartup * 1000); // avoiding Environment.TickCount, which could be negative on long-running platforms
        if (currentMsSinceStart > nextSendTickCount)
        {
            bool doSend = true;
            while (PhotonNetwork.isMessageQueueRunning && doSend)
            {
                // Send all outgoing commands
                Profiler.BeginSample("[PUNVoice]: SendOutgoingCommands");
                doSend = voicePeer.SendOutgoingCommands();
                Profiler.EndSample();
            }

            nextSendTickCount = currentMsSinceStart + updateInterval;
        }
    }

    /// <summary>
    /// Called by Unity when the application gets paused (e.g. on Android when in background).
    /// </summary>
    /// <remarks>
    /// Sets a disconnect timer when PhotonVoiceNetwork.BackgroundTimeout > 0.1f. See PhotonVoiceNetwork.BackgroundTimeout.
    ///
    /// Some versions of Unity will give false values for pause on Android (and possibly on other platforms).
    /// </remarks>
    /// <param name="pause">If the app pauses.</param>
    protected void OnApplicationPause(bool pause)
    {
        if (PhotonVoiceNetwork.BackgroundTimeout > 0.1f)
        {
            if (timerToStopConnectionInBackground == null)
            {
                timerToStopConnectionInBackground = new Stopwatch();
            }
            timerToStopConnectionInBackground.Reset();

            if (pause)
            {
                timerToStopConnectionInBackground.Start();
            }
            else
            {
                timerToStopConnectionInBackground.Stop();
            }
        }
    }

    /// <summary>Called by Unity when the play mode ends. Used to cleanup.</summary>
    protected void OnDestroy()
    {
        StopFallbackSendAckThread();
        //PhotonVoiceNetwork.Disconnect();
    }

    /// <summary>Called by Unity when the application is closed. Disconnects.</summary>
    protected void OnApplicationQuit()
    {
        StopFallbackSendAckThread();
        PhotonVoiceNetwork.Disconnect();
    }
}
