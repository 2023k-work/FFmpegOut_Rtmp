// FFmpegOut - FFmpeg video encoding plugin for Unity
// https://github.com/keijiro/KlakNDI

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Unity.Collections;
using Debug = UnityEngine.Debug;

namespace FFmpegOut
{
    public sealed class FFmpegPipe : System.IDisposable
    {
        #region Public methods

        public static bool IsAvailable
        {
            get { return System.IO.File.Exists(ExecutablePath); }
        }

        public FFmpegPipe(string arguments, bool recordAudio)
        {
            // Start FFmpeg subprocess.
            if (recordAudio)
            {
                _audioPipeThread = new Thread(AudioPipeThread);
                _audioPipeThread.Start();
                UnityEngine.Debug.Log("Audio Pipe Ready to stream.");
            }

            _subprocess = Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });


            _subprocess.ErrorDataReceived += ProcessTheErrorData;
            _subprocess.BeginErrorReadLine();

            // Start copy/pipe subthreads.
            _copyThread = new Thread(CopyThread);
            _pipeThread = new Thread(PipeThread);
            _copyThread.Start();
            _pipeThread.Start();

            void ProcessTheErrorData(object sender, DataReceivedEventArgs e)
            {
                UnityEngine.Debug.LogError(e.Data);
            }
        }


        public void PushFrameData(NativeArray<byte> data)
        {
            // Update the copy queue and notify the copy thread with a ping.
            lock (_copyQueue) _copyQueue.Enqueue(data);
            _copyPing.Set();
        }

        public void PushAudioData(float[] data, int channels)
        {
            lock (_audioPipeQueue) _audioPipeQueue.Enqueue(data);
            _audioReadyPing.Set();
        }

        public void SyncFrameData()
        {
            // Wait for the copy queue to get emptied with using pong
            // notification signals sent from the copy thread.
            while (_copyQueue.Count > 0)
            {
                _copyPong.WaitOne();
                Debug.LogWarning("_copyQueue.Count > 0");
                //break;
            }

            // When using a slower codec (e.g. HEVC, ProRes), frames may be
            // queued too much, and it may end up with an out-of-memory error.
            // To avoid this problem, we wait for pipe queue entries to be
            // comsumed by the pipe thread.
            while (_pipeQueue.Count > 4)
            {
                _pipePong.WaitOne();
                
                Debug.LogWarning("_pipeQueue.Count > 4");
                //break;
            }
        }

        public void CloseAndGetOutput()
        {
            // Terminate the subthreads.
            _terminate = true;

            _copyPing.Set();
            _pipePing.Set();

            _copyThread.Join();
            _pipeThread.Join();
            if (_audioPipeThread != null)
            {
                _audioPipeThread.Join();
            }

            // Close FFmpeg subprocess.
            _subprocess.StandardInput.Close();
            _subprocess.WaitForExit();

            _subprocess.Close();
            _subprocess.Dispose();

            // Nullify members (just for ease of debugging).
            _subprocess = null;
            _copyThread = null;
            _pipeThread = null;
            _copyQueue = null;
            _audioPipeThread = null;
            _pipeQueue = _freeBuffer = null;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            if (!_terminate) CloseAndGetOutput();
        }

        ~FFmpegPipe()
        {
            if (!_terminate)
                UnityEngine.Debug.LogError(
                    "An unfinalized FFmpegPipe object was detected. " +
                    "It should be explicitly closed or disposed " +
                    "before being garbage-collected."
                );
        }

        #endregion

        #region Private members

        Process _subprocess;
        Thread _copyThread;
        Thread _pipeThread;

        Thread _audioPipeThread;

        AutoResetEvent _copyPing = new AutoResetEvent(false);
        AutoResetEvent _copyPong = new AutoResetEvent(false);
        AutoResetEvent _pipePing = new AutoResetEvent(false);
        AutoResetEvent _pipePong = new AutoResetEvent(false);

        AutoResetEvent _audioReadyPing = new AutoResetEvent(false);
        bool _terminate;

        Queue<NativeArray<byte>> _copyQueue = new Queue<NativeArray<byte>>();
        Queue<byte[]> _pipeQueue = new Queue<byte[]>();

        Queue<float[]> _audioPipeQueue = new Queue<float[]>();
        Queue<byte[]> _freeBuffer = new Queue<byte[]>();

        public static string ExecutablePath
        {
            get
            {
                var basePath = UnityEngine.Application.streamingAssetsPath;
                var platform = UnityEngine.Application.platform;

                if (platform == UnityEngine.RuntimePlatform.OSXPlayer ||
                    platform == UnityEngine.RuntimePlatform.OSXEditor)
                    return basePath + "/FFmpegOut/macOS/ffmpeg";

                if (platform == UnityEngine.RuntimePlatform.LinuxPlayer ||
                    platform == UnityEngine.RuntimePlatform.LinuxEditor)
                    return basePath + "/FFmpegOut/Linux/ffmpeg";

                return basePath + "/FFmpegOut/Windows/ffmpeg.exe";
            }
        }

        #endregion

        #region Subthread entry points

        // CopyThread - Copies frames given from the readback queue to the pipe
        // queue. This is required because readback buffers are not under our
        // control -- they'll be disposed before being processed by us. They
        // have to be buffered by end-of-frame.
        void CopyThread()
        {
            while (!_terminate)
            {
                // Wait for ping from the main thread.
                _copyPing.WaitOne();

                // Process all entries in the copy queue.
                while (_copyQueue.Count > 0)
                {
                    // Retrieve an copy queue entry without dequeuing it.
                    // (We don't want to notify the main thread at this point.)
                    NativeArray<byte> source;
                    lock (_copyQueue) source = _copyQueue.Peek();

                    // Try allocating a buffer from the free buffer list.
                    byte[] buffer = null;
                    if (_freeBuffer.Count > 0)
                        lock (_freeBuffer)
                            buffer = _freeBuffer.Dequeue();

                    // Copy the contents of the copy queue entry.
                    if (buffer == null || buffer.Length != source.Length)
                        buffer = source.ToArray();
                    else
                        source.CopyTo(buffer);

                    // Push the buffer entry to the pipe queue.
                    lock (_pipeQueue) _pipeQueue.Enqueue(buffer);
                    _pipePing.Set(); // Ping the pipe thread.

                    // Dequeue the copy buffer entry and ping the main thread.
                    lock (_copyQueue) _copyQueue.Dequeue();
                    _copyPong.Set();
                }
            }
        }

        // PipeThread - Receives frame entries from the copy thread and push
        // them into the FFmpeg pipe.
        void PipeThread()
        {
            var pipe = _subprocess.StandardInput.BaseStream;
            while (!_terminate)
            {
                // Wait for the ping from the copy thread.
                _pipePing.WaitOne();

                // Process all entries in the pipe queue.
                while (_pipeQueue.Count > 0)
                {
                    // Retrieve a frame entry.
                    byte[] buffer;
                    lock (_pipeQueue) buffer = _pipeQueue.Dequeue();

                    // Write it into the FFmpeg pipe.
                    try
                    {
                        pipe.Write(buffer, 0, buffer.Length);
                        pipe.Flush();
                    }
                    catch
                    {
                        // Pipe.Write could raise an IO exception when ffmpeg
                        // is terminated for some reason. We just ignore this
                        // situation and assume that it will be resolved in the
                        // main thread. #badcode
                    }

                    // Add the buffer to the free buffer list to reuse later.
                    lock (_freeBuffer) _freeBuffer.Enqueue(buffer);
                    _pipePong.Set();
                }
            }

        }

        void AudioPipeThread()
        {
            TcpListener server = new TcpListener(System.Net.IPAddress.Any, 50505);
            server.Start();
            // FFMPEG instance connected:
            TcpClient client = server.AcceptTcpClient();
            NetworkStream ns = client.GetStream();

            _audioReadyPing.WaitOne();
            try
            {
                while (!_terminate)
                {
                    while (_audioPipeQueue.Count > 0)
                    {
                        float[] audioBuffer;
                        lock (_audioPipeQueue) audioBuffer = _audioPipeQueue.Dequeue();
                        byte[] audioSendBuffer = new byte[audioBuffer.Length * 4];
                        Buffer.BlockCopy(audioBuffer, 0, audioSendBuffer, 0, audioSendBuffer.Length);
                        ns.Write(audioSendBuffer, 0, audioBuffer.Length * 4);
                    }

                    ns.Flush();
                }

                client.Close();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log("Exception in AudioThread: " + e.ToString());
            }

            client.Close();
            server.Stop();
        }

        #endregion
    }
}