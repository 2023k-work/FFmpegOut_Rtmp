FFmpegOut_Rtmp
=========

**FFmpegOut_Rtmp** is forked from [keijiro/FFmpegOut](https://github.com/keijiro/FFmpegOut)  
It's a Unity plugin that allows the Unity editor and applications to
stream video to Twich and YouTube by using [FFmpeg] .

System Requirements
-------------------

- Unity 2021 or later
- Windows: Direct3D 11

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

RenderTextureLiveStream component
------------------------

The **RenderTextureLiveStream component** (`RenderTextureLiveStream`) is used to capture RenderTexture and streaming.

License
-------

Note that the [FFmpegOutBinaries package] is not placed under this license. 
When distributing an application with the package, it must be taken into
account that multiple licenses are involved. See the [FFmpeg License] page
for further details.

[FFmpeg License]: https://www.ffmpeg.org/legal.html
