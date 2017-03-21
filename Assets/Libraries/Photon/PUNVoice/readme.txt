
This is the readme for the Photon Unity Network Voice Package.
(C) Exit Games GmbH 2016


Overview
----------------------------------------------------------------------------------------------------
The package provides scripts for integration Photon Unity Networking with Photon Voice API

Documentation is available online at this link: http://doc.photonengine.com/en/voice/current/getting-started/voice-for-pun


### Supported platforms

- Standalone: Windows, Mac, Linux
- Windows Store Universal 10
- iOS       (not simulator)
- Android


### Getting started 

Photon Voice for PUN is an add on that brings voice and audio streaming to PUN. It depends on PUN and it requires a separate Photon Realtime application.

Photon Voice for PUN should be imported into a Unity project where PUN is already present and configured.

Note: "Voice room" refers to the room joined by Photon Voice client. It will be used often in this document to avoid confusion with the room joined by PUN client. 
On the other hand, "PUN room" will refer to the room joined by PUN client.

When PUN client is in state "Joined" (in a "PUN room"), it is possible to connect or disconnect Photon Voice application from cloud. 
Other than that, users do not have to explicitly interact with the Photon Voice application. Photon Voice client automatically handles connection workflow. 
It will create or join a "voice room" with the same name as the joined "PUN room".


### Configure

Once you import Photon Voice to the project, a "Photon Voice Settings" section should appear to "PhotonServerSettings" just below PUN settings. 
There you can enter the "AppId" and choose the "Region" of your Photon Voice application. 
Remember that you can always reopen the "PhotonServerSettings" in the inspector by following these steps: "Window" -> "Photon Unity Networking" -> "Highlight Server Settings" or keyboard shortcut Ctrl+Shift+Alt+P. 
 
To tweak advanced audio and voice related settings, make sure a "PhotonVoiceSettings" script is attached to one object in the scene. The available settings are described as follows:  

1. PUN related settings:  
	* "AutoConnect": auto join Photon Voice client to a "voice room" when PUN client is joined to a "PUN room".  
	* "AutoDisconnect": auto disconnects Photon Voice client when PUN client is disconnected.  
	* "AutoTransmit": starts transmitting audio as soon as Photon Voice client is joined to a "voice room".  
2. Microphone and "local" audio recording settings that will be applied to every "recoder" instance created:  
	* "SamplingRate": a frequency of how many times audio is measured per second. Generally, this defines the audio quality you want. 
	Possible values are: 8, 12, 16, 24 and 48 kHz. Default is 24 kHz.  
	* "Delay": outgoing audio stream encoder delay in milliseconds (buffer size in terms of time). Possible values are 5, 10, 20, 40, 80 and 120 ms. Default is 20 ms.  
	* "Bitrate": the amount of data (the number of bits) that are processed over a certain amount of time (second). Generally, this defines the compression quality. Default value is 30000 b/s. 
3. "Remote" audio streaming settings that will be applied to every "speaker" instance created:  
	* "PlayDelayMs": playback delay in milliseconds. Used to compensate incoming packets latency variations.  
4. Voice detection feature (will be applied to every "recoder" instance created):
	* "VoiceDetection": toggle voice detection feature. (read more about this below)
	* "VoiceDetectionThreshold": minimal signal level to be exceeded to start transmission if voice detection is enabled. 0.01 is the default and recommended value.
5. Miscelleanous:  
	* "DebugInfo": toggle Photon Voice debug logs in Unity console at info level.  
	* "DebugLostPercent": lost frames simulation ratio.  

	
### The "audio source" prefab

Photon Voice for PUN expects each "audio source" object to be represented by a prefab.  
The minimal required prefab should contain two script components attached to the same "GameObject": 
  
- "PhotonView"  
- "PhotonVoiceRecorder"   

After adding the "PhotonVoiceRecorder" script, you will notice that an "AudioClip" component has been added and also a "PhotonVoiceSpeaker" script. 
Both are essential to reproducing received remote audio from other clients joined to the same room.  

The prefab should be instantiated only at runtime and after joining a room using PUN.
Scene object instances are not supported. So you should not have any "PhotonVoiceRecorder" instances in the scene.  

The audio source prefab is used for two different purposes:  

1. Record and transmit "local" sound:  
This is handled by "PhotonVoiceRecorder".  
2. Reproduce and play received "remote" sound:  
This is managed by "PhotonVoiceSpeaker".  

This audio source prefab can be used in two different ways:  

1. Voice chat:  
If no audio clip is assigned to "PhotonVoiceRecorder" and if a microphone device is detected, audio will be recorded and transmitted to all other joined players. 
In case multiple recording devices are available, the one to be used need to be set to "PhotonVoiceRecorder.MicrophoneDevice" property.  
2. Streaming audio clips:  
This option was introduced to test Photon Voice transmission. However, you may use it for other purposes. 
In order to broadcast an audio clip to other players joined to the same room, you need to assign an audio media file supported by Unity to "PhotonVoiceRecorder.AudioClip". 
This can be done by a drag and drop to the Inspector from Unity Editor. If you want the audio clip to be replayed every time make sure "PhotonVoiceRecorder.Loop" is set to "true".  


### Voice detection 

Voice detection is an optional feature that will filter recorded sound and transmits only when a predefined threshold of signal level is exceeded. 
This means that voice transmission is automatically paused when you stop speaking and it is resumed when you start talking. 
This will also help you avoid sending useless noise and thus reduce bandwidth consumption.

The default value of "VoiceDetectionThreshold" is 0,01. 
It is the recommended value for common environments as a result of experiments with voice detection calibration and noise level measurements. 


### Voice calibration
 
If you still experience issues when recording your voice even with voice detection is on, you may need voice calibration. 
Photon Voice offers an auto calibration mode which is limited in time. 
To start calibration mode you should call "PhotonVoiceRecorder.VoiceDetectorCalibrate(period)". 
The default calibration period is 2000 milliseconds but you can always change it. 
Using calibration, Photon Voice automatically adjusts silence and loundess thresholds.


### Push-to-Talk

It is easy to have a push-to-talk feature with Photon Voice.
You need to manually toggle voice recording and transmission.
It's like turning microphone mute on and off.
To start push-to-talk just set "PhotonVoiceRecorder.Transmit" to "true". 
To stop it just set the value back to "false".
You can achieve this by binding the property to a user input (UI button or keyboard shortcut).


### Known issues

#### Android 6.0 permissions

Usually when building for Android platform, most permissions will be added by automatically by Unity to the manifest file.
The two permissions required by Photon Voice are:

- "android.permission.INTERNET": automatically added via Unity's Player Settings.  
- "android.permission.RECORD_AUDIO": automatically added when Unity's microphone API is used.  

However, with Android Marshmallow there seems to be a problem due to the newly introduced permissions model. 
The generated APK will be missing "android.permission.RECORD_AUDIO" permission and the application will not be able to record your voice.
To avoid such issue please update to the latest Unity version when targetting Android SDK level 23.