// -----------------------------------------------------------------------
// <copyright file="VoiceInfo.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2017 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------
//#define PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
#if NETFX_CORE
using Windows.System.Threading;
#endif
namespace ExitGames.Client.Photon.Voice
{
    /// <summary>Describes stream properties.</summary>
    public struct VoiceInfo
    {
        /// <summary>
        /// Helper for Opus stream info creation.
        /// </summary>
        /// <param name="samplingRate">Audio sampling rate.</param>
        /// <param name="sourceSamplingRate">Source audio sampling rate (to be resampled to SamplingRate).</param>
        /// <param name="channels">Number of channels.</param>
        /// <param name="frameDurationUs">Uncompressed frame (audio packet) size in microseconds.</param>
        /// <param name="bitrate">Stream bitrate.</param>
        /// <param name="userdata">Optional user data. Should be serializable by Photon.</param>
        /// <returns>VoiceInfo instance.</returns>
        static public VoiceInfo CreateAudioOpus(POpusCodec.Enums.SamplingRate samplingRate, int sourceSamplingRate, int channels, OpusCodec.FrameDuration frameDurationUs, int bitrate, object userdata = null)
        {
            return new VoiceInfo()
            {
                Codec = Codec.AudioOpus,
                SamplingRate = (int)samplingRate,
                SourceSamplingRate = sourceSamplingRate,
                Channels = channels,
                FrameDurationUs = (int)frameDurationUs,
                Bitrate = bitrate,
                UserData = userdata
            };
        }
#if PHOTON_VOICE_VIDEO_ENABLE
        /// <summary>
        /// Helper for VP8 stream info creation.
        /// </summary>
        /// <param name="bitrate">Stream bitrate.</param>
        /// <param name="width">Streamed video width. If 0, width and height of video source used (no rescaling).</param>
        /// <param name="heigth">Streamed video height. If -1, aspect ratio preserved during rescaling.</param>
        /// <param name="userdata">Optional user data. Should be serializable by Photon.</param>        
        /// <returns>VoiceInfo instance.</returns>
        static public VoiceInfo CreateVideoVP8(int bitrate, int width = 0, int heigth = -1, object userdata = null)
        {
            return new VoiceInfo()
            {
                Codec = Codec.VideoVP8,
                Bitrate = bitrate,
                Width = width,
                Height = heigth,
                UserData = userdata,
            };
        }
#endif
        public override string ToString()
        {
            return "c=" + Codec + " f=" + SamplingRate + " ch=" + Channels + " d=" + FrameDurationUs + " s=" + FrameSize + " b=" + Bitrate + " w=" + Width + " h=" + Height + " ud=" + UserData;
        }
        internal static VoiceInfo CreateFromEventPayload(Dictionary<byte, object> h)
        {
            var i = new VoiceInfo();
            i.SamplingRate = (int)h[(byte)EventParam.SamplingRate];
            i.Channels = (int)h[(byte)EventParam.Channels];
            i.FrameDurationUs = (int)h[(byte)EventParam.FrameDurationUs];
            i.Bitrate = (int)h[(byte)EventParam.Bitrate];
            i.UserData = h[(byte)EventParam.UserData];
            i.Codec = (Codec)h[(byte)EventParam.Codec];
            return i;
        }
        public Codec Codec { get; set; }
        /// <summary>Audio sampling rate (frequency).</summary>
        public int SamplingRate { get; set; }
        /// <summary>Source audio sampling rate (to be resampled to SamplingRate).</summary>
        public int SourceSamplingRate { get; set; }
        /// <summary>Number of channels.</summary>
        public int Channels { get; set; }
        /// <summary>Uncompressed frame (audio packet) size in microseconds.</summary>
        public int FrameDurationUs { get; set; }
        /// <summary>Compression quality in terms of bits per second.</summary>
        public int Bitrate { get; set; }
        /// <summary>Optional user data. Should be serializable by Photon.</summary>
        public object UserData { get; set; }
        /// <summary>Uncompressed frame (data packet) size in samples.</summary>
        public int FrameDurationSamples { get { return (int)(this.SamplingRate * (long)this.FrameDurationUs / 1000000); } }
        /// <summary>Uncompressed frame (data packet) size in samples.</summary>
        public int FrameSize { get { return this.FrameDurationSamples * this.Channels; } }
        /// <summary>Video width (optional).</summary>
        public int Width { get; set; }
        /// <summary>Video height (optional)</summary>
        public int Height { get; set; }
    }
    /// <summary>Helper to provide remote voices infos via Client.RemoteVoiceInfos iterator.</summary>
    public class RemoteVoiceInfo
    {
        internal RemoteVoiceInfo(int channelId, int playerId, byte voiceId, VoiceInfo info, object localUserObject)
        {
            this.ChannelId = channelId;
            this.PlayerId = playerId;
            this.VoiceId = voiceId;
            this.Info = info;
            this.LocalUserObject = localUserObject;
        }
        /// <summary>Remote voice info.</summary>
        public VoiceInfo Info { get; private set; }
        /// <summary>Id of channel used for transmission.</summary>
        public int ChannelId { get; private set; }
        /// <summary>Player Id of voice owner.</summary>
        public int PlayerId { get; private set; }
        /// <summary>Voice id unique in the room.</summary>
        public byte VoiceId { get; private set; }
        /// <summary>Object set by user when remote voice created.</summary>
        public object LocalUserObject { get; private set; }
    }
}
