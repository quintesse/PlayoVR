using UnityEngine;
using System.Collections;
using System;
using System.IO;
using Oculus.Avatar;

public class RemoteLoopbackManager : MonoBehaviour {

    public OvrAvatar LocalAvatar;
    public OvrAvatar LoopbackAvatar;

	void Start () {
        LocalAvatar.RecordPackets = true;
        LocalAvatar.PacketRecorded += OnLocalAvatarPacketRecorded;
	}

    void OnLocalAvatarPacketRecorded(object sender, OvrAvatar.PacketEventArgs args)
    {
        var size = CAPI.ovrAvatarPacket_GetSize(args.Packet.ovrNativePacket);
        byte[] data = new byte[size];
        CAPI.ovrAvatarPacket_Write(args.Packet.ovrNativePacket, size, data);
        SendPacketData(data);
    }

    void SendPacketData(byte[] data)
    {
        // Loopback by just "receiving" the data
        ReceivePacketData(data);
    }

    void ReceivePacketData(byte[] data)
    {
        IntPtr packet = CAPI.ovrAvatarPacket_Read((UInt32)data.Length, data);
        LoopbackAvatar.GetComponent<OvrAvatarRemoteDriver>().QueuePacket(0, new OvrAvatarPacket{ ovrNativePacket = packet });
    }
}