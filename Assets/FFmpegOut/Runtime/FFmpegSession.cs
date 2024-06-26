// FFmpegOut - FFmpeg video encoding plugin for Unity
// https://github.com/keijiro/KlakNDI

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace FFmpegOut
{
    public sealed class FFmpegSession : System.IDisposable
    {
        #region Factory methods

        public static FFmpegSession Create(
            string name,
            int width, int height, float frameRate,
            FFmpegPreset preset, bool recordAudio
        )
        {
            name += System.DateTime.Now.ToString(" yyyy MMdd HHmmss");
            var path = name.Replace(" ", "_") + preset.GetSuffix();
            return CreateWithOutputPath(path, width, height, frameRate, preset,
                recordAudio);
        }

        public static FFmpegSession CreateWithOutputPath(
            string outputPath,
            int width, int height, float frameRate,
            FFmpegPreset preset, bool recordAudio
        )
        {
/*
@"-y -framerate 30 -f rawvideo -pix_fmt rgb32 -video_size 800x600 "
      + @"-i unix:///var/folders/8q/p8gcyljs02lg80hxvyv49c800000gn/T/CoreFxPipe_ffv "
     +@"-f s16le -channels 1 -sample_rate 48000 "
      + @"-i unix:///var/folders/8q/p8gcyljs02lg80hxvyv49c800000gn/T/CoreFxPipe_ffa "
     +@"-map 0:0 -map 1:0 -vcodec libx264 -crf 23 -pix_fmt yuv420p -preset medium -r 30 -c:a aac out.mp4";




           @"-y -v 9 -loglevel 99 " +
      @" -thread_queue_size 512 -framerate "+out_fps+" -f rawvideo -pix_fmt rgb32 -video_size 800x600 " +
      @"-i - " +
      @"-f s16le -ac 1 -ar "+out_ar+" -thread_queue_size 512 " +
      @"-i async:tcp://127.0.0.1:50505 -loglevel trace " +
      @"-map 1:0 -map 0:0 -c:a aac -ac 1 -ar 48000 " + // -map 0:0 -map 1:0
      @"-vcodec libx264 -crf 23 -pix_fmt yuv420p -preset ultrafast -r "+out_fps+" out.mp4"

*/
            string videoPipeName = "-";
            string audioPipeName = "async:tcp://127.0.0.1:50505?timeout=5000000";
            string audioInputSpecification = " ";
            string audioOutputOptions = " "; // TODO: presets
            if (recordAudio) {
                // add audio pipe
                
                //,(int)AudioSettings.speakerMode,AudioSettings.outputSampleRate
                audioInputSpecification = $" -f f32le -ac {(int)AudioSettings.speakerMode} -ar "+ AudioSettings.outputSampleRate
                    + " -thread_queue_size 512 -i " 
                    + audioPipeName;
                audioOutputOptions = " -map 0:0 -map 1:0 -c:a aac -ac 2 ";
            }
            string args = "-y -f rawvideo -thread_queue_size 512 -vcodec rawvideo -pixel_format rgba"
                + " -colorspace bt709"
                + " -video_size " + width + "x" + height
                + " -framerate " + frameRate
                + " -loglevel warning -i " + videoPipeName
                + audioInputSpecification
                + audioOutputOptions
                + preset.GetOptions()
                + " \"" + outputPath + "\"";
            UnityEngine.Debug.Log(args);
            return new FFmpegSession(args, recordAudio);
        }

        public static FFmpegSession CreateLiveStream(
            string url, int width, int height, float frameRate,
            bool recordAudio
        )
        {
            string videoPipeName = "-";
            string audioPipeName = "async:tcp://127.0.0.1:50505?timeout=5000000";
            string audioInputSpecification = " ";
            string audioOutputOptions = " "; // TODO: presets
            if (recordAudio) {
                // add audio pipe
                
                //,(int)AudioSettings.speakerMode,AudioSettings.outputSampleRate
                audioInputSpecification = $" -f f32le -ac {(int)AudioSettings.speakerMode} -ar "+ AudioSettings.outputSampleRate
                    + " -thread_queue_size 512 -i " 
                    + audioPipeName;
                audioOutputOptions = " -map 0:0 -map 1:0 -c:a aac -ac 2 ";
            }

            string args = "-y -f rawvideo -thread_queue_size 512 -vcodec rawvideo -pixel_format rgba"
                          + " -colorspace bt709"
                          + " -video_size " + width + "x" + height
                          + " -framerate " + frameRate
                          + " -loglevel warning -i " + videoPipeName
                          + audioInputSpecification
                          + audioOutputOptions
                          + " -vcodec libx264 -pix_fmt yuv420p -preset:v ultrafast -tune:v zerolatency -x264opts keyint=50 -f flv " //-g 25
                          + url;
            UnityEngine.Debug.Log(args);

            return new FFmpegSession(args, recordAudio);
        }

        public static FFmpegSession CreateWithArguments(string arguments, bool recordAudio)
        {
            return new FFmpegSession(arguments, recordAudio);
        }

        #endregion

        #region Public properties and members

        public void PushFrame(Texture source)
        {
            if (_pipe != null)
            {
                _framePushed = true;
                ProcessQueue();
                if (source != null) QueueFrame(source);
            }
        }
        
        public void PushAudioBuffer(float[] buffer, int channels) {
            
            if (_pipe != null&&_framePushed)
            {
                _pipe.PushAudioData(buffer, channels);
            }
        }

        public void CompletePushFrames()
        {
            //_pipe?.SyncFrameData();
        }

        public void Close()
        {
            if (_pipe != null)
            {
                _pipe.CloseAndGetOutput();

                _pipe.Dispose();
                _pipe = null;
            }

            if (_blitMaterial != null)
            {
                UnityEngine.Object.Destroy(_blitMaterial);
                _blitMaterial = null;
            }
        }

        public void Dispose()
        {
            Close();
        }

        #endregion

        #region Private objects and constructor/destructor

        FFmpegPipe _pipe;
        Material _blitMaterial;

        public readonly bool recordAudio;
        private bool _framePushed;
        
        FFmpegSession(string arguments, bool recordAudio)
        {
            this.recordAudio = recordAudio;
            if (!FFmpegPipe.IsAvailable)
                Debug.LogWarning(
                    "Failed to initialize an FFmpeg session due to missing " +
                    "executable file. Please check FFmpeg installation."
                );
            else if (!UnityEngine.SystemInfo.supportsAsyncGPUReadback)
                Debug.LogWarning(
                    "Failed to initialize an FFmpeg session due to lack of " +
                    "async GPU readback support. Please try changing " +
                    "graphics API to readback-enabled one."
                );
            else
                _pipe = new FFmpegPipe(arguments, recordAudio);
        }


        ~FFmpegSession()
        {
            if (_pipe != null)
                Debug.LogError(
                    "An unfinalized FFmpegCapture object was detected. " +
                    "It should be explicitly closed or disposed " +
                    "before being garbage-collected."
                );
        }

        #endregion

        #region Frame readback queue

        List<AsyncGPUReadbackRequest> _readbackQueue =
            new List<AsyncGPUReadbackRequest>(4);

        void QueueFrame(Texture source)
        {
            if (_readbackQueue.Count > 6)
            {
                Debug.LogWarning("Too many GPU readback requests.");
                return;
            }

            // Lazy initialization of the preprocessing blit shader
            if (_blitMaterial == null)
            {
                var shader = Shader.Find("Hidden/FFmpegOut/Preprocess");
                _blitMaterial = new Material(shader);
            }

            // Blit to a temporary texture and request readback on it.
            var rt = RenderTexture.GetTemporary
                (source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt, _blitMaterial, 0);
            _readbackQueue.Add(AsyncGPUReadback.Request(rt));
            RenderTexture.ReleaseTemporary(rt);
        }

        void ProcessQueue()
        {
            while (_readbackQueue.Count > 0)
            {
                // Check if the first entry in the queue is completed.
                if (!_readbackQueue[0].done)
                {
                    // Detect out-of-order case (the second entry in the queue
                    // is completed before the first entry).
                    if (_readbackQueue.Count > 1 && _readbackQueue[1].done)
                    {
                        // We can't allow the out-of-order case, so force it to
                        // be completed now.
                        _readbackQueue[0].WaitForCompletion();
                    }
                    else
                    {
                        // Nothing to do with the queue.
                        break;
                    }
                }

                // Retrieve the first entry in the queue.
                var req = _readbackQueue[0];
                _readbackQueue.RemoveAt(0);

                // Error detection
                if (req.hasError)
                {
                    Debug.LogWarning("GPU readback error was detected.");
                    continue;
                }

                // Feed the frame to the FFmpeg pipe.
                _pipe.PushFrameData(req.GetData<byte>());
            }
        }

        #endregion
    }
}
