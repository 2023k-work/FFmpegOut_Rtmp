using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace FFmpegOut
{
    public class RenderTextureLiveStream : MonoBehaviour
    {
        public string Url;
        public bool StreamCameraOnEnable;
        public bool StreamCameraAudio;
        private FFmpegSession _session;
        private RenderTexture _streamRt;

        public bool IsStreaming => _session != null;


        private void Update()
        {
            _session?.PushFrame(_streamRt);
        }

        private void OnEnable()
        {
            if (StreamCameraOnEnable)
            {
                var cam = GetComponent<Camera>();

                if (cam == null)
                {
                    Debug.LogError("Need camera component");
                    return;
                }

                if (cam.targetTexture == null)
                {
                    Debug.LogError("Camera targetTexture is null");
                    return;
                }

                StartStream(cam.targetTexture, StreamCameraAudio);
            }
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                // Close and dispose the FFmpeg session.
                _streamRt = null;
                _session.Close();
                _session.Dispose();
                _session = null;
            }
        }

        private void OnAudioFilterRead(float[] buffer, int channels)
        {
            if (_session != null && _session.recordAudio && StreamCameraAudio)
                _session?.PushAudioBuffer(buffer, channels);
        }

        public void StartStream(RenderTexture rt, bool recordAudio)
        {
            if (rt == null)
            {
                return;
            }

            _streamRt = rt;

            _session = FFmpegSession.CreateLiveStream(
                Url,
                _streamRt.width,
                _streamRt.height,
                30,
                recordAudio
            );
            StartCoroutine(OnEndOfFrame());
        }
        
        private void PushAudioData(float[] buffer, int channels)
        {
            if (_session != null && _session.recordAudio)
                _session?.PushAudioBuffer(buffer, channels);
        }

        public void Stop()
        {
            OnDisable();
        }
        


        private IEnumerator OnEndOfFrame()
        {
            // Sync with FFmpeg pipe thread at the end of every frame.
            for (var eof = new WaitForEndOfFrame();;)
            {
                yield return eof;
                _session?.CompletePushFrames();
            }
        }
    }
}