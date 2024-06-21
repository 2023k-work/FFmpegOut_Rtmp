FFmpegOut_Rtmp
=========

**FFmpegOut_Rtmp** is forked from [keijiro/FFmpegOut](https://github.com/keijiro/FFmpegOut)
It's a Unity plugin that allows the Unity editor and applications to
stream video to Twich and YouTube by using [FFmpeg] .

System Requirements
-------------------

- Unity 2018.3 or later
- Windows: Direct3D 11
- macOS: Metal
- Linux: Vulkan

FFmpegOut only supports desktop platforms.

FFmpegOut works not only on the legacy rendering paths (forward/deferred) but
also on the standard scriptable render pipelines (LWRP/HDRP).

Installation
------------

Download and import the following packages into your project.

- [FFmpegOut package] (MIT license)
- [FFmpegOutBinaries package] (GPL)

[FFmpegOut package]: https://github.com/keijiro/FFmpegOut/releases
[FFmpegOutBinaries package]:
    https://github.com/keijiro/FFmpegOutBinaries/releases

Camera Capture component
------------------------

The **Camera Capture component** (`CameraCapture`) is used to capture frames
rendered by an attached camera.

![inspector](https://i.imgur.com/M4fxPov.png)

It has a few properties for recording video: frame dimensions, preset and frame
rate.

[Application.targetFrameRate]:
    https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html
[QualitySettings.vSyncCount]:
    https://docs.unity3d.com/ScriptReference/QualitySettings-vSyncCount.html
[Time.captureFramerate]:
    https://docs.unity3d.com/ScriptReference/Time-captureFramerate.html

License
-------

[MIT](LICENSE.md)

Note that the [FFmpegOutBinaries package] is not placed under this license. 
When distributing an application with the package, it must be taken into
account that multiple licenses are involved. See the [FFmpeg License] page
for further details.

[FFmpeg License]: https://www.ffmpeg.org/legal.html
