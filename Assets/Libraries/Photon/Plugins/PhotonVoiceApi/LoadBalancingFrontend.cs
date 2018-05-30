// -----------------------------------------------------------------------
// <copyright file="Client.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2015 Exit Games GmbH
// </copyright>
// <summary>
//   Extends Photon LoadBalancing API with audio streaming functionality.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitGames.Client.Photon.Voice
{
    /// <summary>
    /// Different channels voices events are sent to diferent enet channels.
    /// Single event code is enough for all voice communications.
    /// Because of EventData does not provides ChannelID, we are forced to encode channel id in event code.
    /// For this purpose, range of event codes of PhotonPeer.ChannelCount length starting from Code0 used.
    /// </summary>{
    class VoiceEventCode
    {
        /// <summary>
        /// Start of voice event codes range
        /// Change if conflicts with other code.
        /// </summary>{    
        public const byte Code0 = 201;
        static public byte GetCode(int channelID)
        {
            return (byte)(Code0 + channelID);
        }
        static public bool TryGetChannelID(byte evCode, int maxChannels, out byte channelID)
        {
            if (evCode >= Code0 && evCode < Code0 + maxChannels)
            {
                channelID = (byte)(evCode - Code0);
                return true;
            }
            else
            {
                channelID = 0;
                return false;
            }
        }
    }

    /// <summary>
    /// This class extends LoadBalancingClient with audio streaming functionality.
    /// </summary>
    /// <remarks>
    /// Use LoadBalancing workflow to join Voice room. All standard LoadBalancing features available.
    /// To work with audio:
    /// Create outgoing audio streams with Client.CreateLocalVoice method.
    /// Handle new incoming audio streams info with Client.OnRemoteVoiceInfoAction.
    /// Handle incoming audio streams data with Client.OnAudioFrameAction.
    /// </remarks>
    public class LoadBalancingFrontend : LoadBalancing.LoadBalancingClient, IVoiceFrontend, IDisposable
    {
        public VoiceClient VoiceClient { get { return this.voiceClient; } }
        protected VoiceClient voiceClient;

        public void LogError(string fmt, params object[] args) { this.DebugReturn(DebugLevel.ERROR, string.Format(fmt, args)); }
        public void LogWarning(string fmt, params object[] args) { this.DebugReturn(DebugLevel.WARNING, string.Format(fmt, args)); }
        public void LogInfo(string fmt, params object[] args) { this.DebugReturn(DebugLevel.INFO, string.Format(fmt, args)); }
        public void LogDebug(string fmt, params object[] args) { this.DebugReturn(DebugLevel.ALL, string.Format(fmt, args)); }

        // Assigns channel per known voice type reserving channel 0 for user events. 
        // For more control, set channel explicitly when creating a voice.
        public int AssignChannel(VoiceInfo v)
        {
            // 0 is for user events
            return 1 + Array.IndexOf(Enum.GetValues(typeof(Codec)), v.Codec);            
        }

        public bool IsChannelJoined(int channelId) { return this.State == LoadBalancing.ClientState.Joined; }

        public void SetDebugEchoMode(LocalVoice v)
        {
            if (this.State == LoadBalancing.ClientState.Joined)
            {
                if (v.DebugEchoMode)
                {
                    SendVoicesInfo(new List<LocalVoice>() { v }, v.channelId, this.LocalPlayer.ID);
                }
                else
                {
                    SendVoiceRemove(v, v.channelId, this.LocalPlayer.ID);
                }
            }
        }

        // let user code set actions which we occupy; call them in our actions
        /// <summary>Register a method to be called when an event got dispatched. Gets called at the end of OnEvent().</summary>
        /// <see cref="ExitGames.Client.Photon.LoadBalancing.LoadBalancingClient.OnEventAction"/>
        new public Action<EventData> OnEventAction { get; set; } // called by voice client action, so user still can use action

        // let user code set actions which we occupy; call them in our actions
        /// <summary>Register a method to be called when an event got dispatched. Gets called at the end of OnEvent().</summary>
        /// <see cref="ExitGames.Client.Photon.LoadBalancing.LoadBalancingClient.OnStateChangeAction"/>
        new public Action<LoadBalancing.ClientState> OnStateChangeAction { get; set; } // called by voice client action, so user still can use action
        
        /// <summary>Creates Client instance</summary>
        public LoadBalancingFrontend(ConnectionProtocol connectionProtocol = ConnectionProtocol.Udp) : base(connectionProtocol)
        {
            base.OnEventAction += onEventActionVoiceClient;
            base.OnStateChangeAction += onStateChangeVoiceClient;
            this.voiceClient = new VoiceClient(this);
            var voiceChannelsCount = Enum.GetValues(typeof(Codec)).Length + 1; // channel per stream type, channel 0 is for user events
            if (loadBalancingPeer.ChannelCount < voiceChannelsCount)
            {
                this.loadBalancingPeer.ChannelCount = (byte)voiceChannelsCount;
            }
        }

        /// <summary>
        /// This method dispatches all available incoming commands and then sends this client's outgoing commands.
        /// Call this method regularly (2..20 times a second).
        /// </summary>
        new public void Service()
        {
            base.Service();
            this.voiceClient.Service();
        }

        /// <summary>
        /// Change audio groups listended by client. Works only while joined to a voice room.
        /// </summary>
        /// <see cref="LocalVoice.Group"/>
        /// <see cref="SetGlobalAudioGroup(byte)"/>
        /// <remarks>
        /// Note the difference between passing null and byte[0]:
        ///   null won't add/remove any groups.
        ///   byte[0] will add/remove all (existing) groups.
        /// First, removing groups is executed. This way, you could leave all groups and join only the ones provided.
        /// </remarks>
        /// <param name="groupsToRemove">Groups to remove from listened. Null will not leave any. A byte[0] will remove all.</param>
        /// <param name="groupsToAdd">Groups to add to listened. Null will not add any. A byte[0] will add all current.</param>
        /// <returns>If request could be enqueued for sending</returns>
        public virtual bool ChangeAudioGroups(byte[] groupsToRemove, byte[] groupsToAdd)
        {
            return this.loadBalancingPeer.OpChangeGroups(groupsToRemove, groupsToAdd);
        }

        /// <summary>
        /// Set global audio group for this client. This call sets AudioGroup for existing local voices and for created later to given value.
        /// Client set as listening to this group only until ChangeAudioGroups called. This method can be called any time.
        /// </summary>
        /// <see cref="LocalVoice.Group"/>
        /// <see cref="ChangeAudioGroups(byte[], byte[])"/>
        public byte GlobalAudioGroup
        {
            get { return this.voiceClient.GlobalGroup; }
            set
            {
                this.voiceClient.GlobalGroup = value;
                if (this.State == LoadBalancing.ClientState.Joined)
                {
                    if (this.voiceClient.GlobalGroup != 0)
                    {
                        this.loadBalancingPeer.OpChangeGroups(new byte[0], new byte[] { this.voiceClient.GlobalGroup });
                    }
                    else
                    {
                        this.loadBalancingPeer.OpChangeGroups(new byte[0], null);
                    }
                }                
            }
        }


        #region nonpublic

        object sendLock = new object();

        //
        public void SendVoicesInfo(IEnumerable<LocalVoice> voices, int channelId, int targetPlayerId)
        {
            object content = voiceClient.buildVoicesInfo(voices, true);

            var sendOpt = new SendOptions()
            {
                Reliability = true,
                Channel = (byte)channelId
            };

        var opt = new LoadBalancing.RaiseEventOptions();
            if (targetPlayerId != 0)
            {
                opt.TargetActors = new int[] { targetPlayerId };
            }
            lock (sendLock)
            {
                this.OpRaiseEvent(VoiceEventCode.GetCode(channelId), content, opt, sendOpt);
            }

            if (targetPlayerId == 0) // send debug echo infos to myself if broadcast requested
            {
                SendDebugEchoVoicesInfo(channelId);
            }
        }

        public void SendDebugEchoVoicesInfo(int channelId)
        {
            var voices = voiceClient.LocalVoices.Where(x => x.DebugEchoMode);
            if (voices.Count() > 0)
            { 
                SendVoicesInfo(voices, channelId, this.LocalPlayer.ID);
            }
        }

        public void SendVoiceRemove(LocalVoice voice, int channelId, int targetPlayerId)
        {
            object content = voiceClient.buildVoiceRemoveMessage(voice);
            var sendOpt = new SendOptions()
            {
                Reliability = true,
                Channel = (byte)channelId
            };

            var opt = new LoadBalancing.RaiseEventOptions();
            if (targetPlayerId != 0)
            {
                opt.TargetActors = new int[] { targetPlayerId };
            }
            lock (sendLock)
            {
                this.OpRaiseEvent(VoiceEventCode.GetCode(channelId), content, opt, sendOpt);
            }
        }

        public void SendFrame(ArraySegment<byte> data, byte evNumber, byte voiceId, int channelId, LocalVoice localVoice)
        {
            object[] content = new object[] { voiceId, evNumber, data };

            var sendOpt = new SendOptions()
            {
                Reliability = localVoice.Reliable,
                Channel = (byte)channelId
            };

            var opt = new LoadBalancing.RaiseEventOptions();
            if (localVoice.DebugEchoMode)
            {
                opt.Receivers = LoadBalancing.ReceiverGroup.All;
            }
            opt.InterestGroup = localVoice.Group;
            lock (sendLock)
            {
                this.OpRaiseEvent((byte)VoiceEventCode.GetCode(channelId), content, opt, sendOpt);
            }
            this.loadBalancingPeer.SendOutgoingCommands();
        }

        public string ChannelIdStr(int channelId) { return null; }
        public string PlayerIdStr(int playerId) { return null; }
        private void onEventActionVoiceClient(EventData ev)
        {
            byte channel;
            // check for voice event first
            if (VoiceEventCode.TryGetChannelID(ev.Code, this.loadBalancingPeer.ChannelCount, out channel))
            {
                // Payloads are arrays. If first array element is 0 than next is event subcode. Otherwise, the event is data frame with voiceId in 1st element.                    
                this.voiceClient.onVoiceEvent(ev[(byte)LoadBalancing.ParameterCode.CustomEventContent], channel, (int)ev[LoadBalancing.ParameterCode.ActorNr], this.LocalPlayer.ID);
            }
            else
            {
                int playerId;
                switch (ev.Code)
                {
                    case (byte)LoadBalancing.EventCode.Join:
                        playerId = (int)ev[LoadBalancing.ParameterCode.ActorNr];
                        if (playerId == this.LocalPlayer.ID)
                        {
                        }
                        else
                        {
                            this.voiceClient.sendVoicesInfo(playerId);// send to new joined only
                        }
                        break;
                    case (byte)LoadBalancing.EventCode.Leave:
                        {
                            playerId = (int)ev[LoadBalancing.ParameterCode.ActorNr];
                            if (playerId == this.LocalPlayer.ID)
                            {
                                this.voiceClient.clearRemoteVoices();
                            }
                            else
                            {
                                onPlayerLeave(playerId);
                            }
                        }
                        break;
                }
            }
            if (this.OnEventAction != null) this.OnEventAction(ev);
        }

        void onStateChangeVoiceClient(LoadBalancing.ClientState state)
        {
            switch (state)
            {
                case LoadBalancing.ClientState.Joined:
                    this.voiceClient.clearRemoteVoices();
                    this.voiceClient.sendVoicesInfo(0);// my join, broadcast
                    if (this.voiceClient.GlobalGroup != 0)
                    {
                        this.loadBalancingPeer.OpChangeGroups(new byte[0], new byte[] { this.voiceClient.GlobalGroup });
                    }
                    break;
                case LoadBalancing.ClientState.Disconnected:
                    this.voiceClient.clearRemoteVoices();
                    break;
            }
            if (this.OnStateChangeAction != null) this.OnStateChangeAction(state);
        }
        private void onPlayerLeave(int playerId)
        {
            if (this.voiceClient.removePlayerVoices(playerId))
            {
                this.DebugReturn(DebugLevel.INFO, "[PV] Player " + playerId + " voices removed on leave");
            }            
            else
            {
                this.DebugReturn(DebugLevel.WARNING, "[PV] Voices of player " + playerId + " not found when trying to remove on player leave");
            }
        }
#endregion

        public void Dispose()
        {
            this.voiceClient.Dispose();
        }
    }
}