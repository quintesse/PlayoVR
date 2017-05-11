using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Oculus.Avatar;

public class OvrAvatarRemoteDriver : OvrAvatarDriver
{
    Queue<OvrAvatarPacket> packetQueue = new Queue<OvrAvatarPacket>();

    IntPtr CurrentSDKPacket = IntPtr.Zero;
    float CurrentSDKPacketTime = 0f;
  
    public void QueuePacket(int sequence, OvrAvatarPacket packet)
    {
        packetQueue.Enqueue(packet);
    }

    public override void UpdateTransforms(IntPtr sdkAvatar)
    {
        if (packetQueue.Count <= 0)
        {
            return;
        }

        if (CurrentSDKPacket == IntPtr.Zero)
        {
            CurrentSDKPacket = packetQueue.Dequeue().ovrNativePacket;
        }

        if (CurrentSDKPacket != IntPtr.Zero)
        {
            float PacketDuration = CAPI.ovrAvatarPacket_GetDurationSeconds(CurrentSDKPacket);
            CAPI.ovrAvatar_UpdatePoseFromPacket(sdkAvatar, CurrentSDKPacket, Mathf.Min(PacketDuration, CurrentSDKPacketTime));
            CurrentSDKPacketTime += Time.deltaTime;

            if (CurrentSDKPacketTime > PacketDuration)
            {
                CAPI.ovrAvatarPacket_Free(CurrentSDKPacket);
                CurrentSDKPacket = IntPtr.Zero;
                CurrentSDKPacketTime = CurrentSDKPacketTime - PacketDuration;
            }
        }
    }
}