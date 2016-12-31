﻿using JWLibrary.FFmpeg.Properties;
using System;
using System.IO;
using System.Reflection;

namespace JWLibrary.FFmpeg
{
    public class FFMpegCaptureAV : IDisposable
    {
        #region delegate events
        public event EventHandler<System.Diagnostics.DataReceivedEventArgs> DataReceived;
        protected virtual void OnDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (DataReceived != null)
            {
                DataReceived(this, e);
            }
        }

        public event EventHandler<EventArgs> FrameDroped;
        protected virtual void OnFrameDroped(object sender, EventArgs e)
        {
            if (FrameDroped != null)
            {
                FrameDroped(this, e);
            }
        }
        #endregion

        #region variable
        bool disposed = false;
        private CLIHelper _cmdHelper;
        private FrameDropChecker _frameDropChecker;

        private const string FFMPEG_PROCESS_NAME = "ffmpeg";
        private const string FFMPEG_FILE_NAME = "ffmpeg.exe";
        private const string STOP_COMMAND = "q";
        private const string DROP_KEYWORD = "FRAME DROPPED!";
        private const string AUDIO_SNIFFER_FILE_NAME = "audio_sniffer.dll";        
        private const string SCREEN_CAPTURE_RECORDER_FILE_NAME = "screen_capture_recorder.dll";
        private const string LIBRARY_REGISTER_FILE_NAME = "library-register.bat";
        private const string LIBRARY_UNREGISTER_FILE_NAME = "library-unregister.bat";
        #endregion

        #region constructor
        public FFMpegCaptureAV()
        {
            this._cmdHelper = new CLIHelper();
            this._frameDropChecker = new FrameDropChecker();
            this._cmdHelper.CommandDataReceived += cmdHelper_CommandDataReceived;
            this._frameDropChecker.FrameDroped += _frameDropChecker_FrameDroped;
        }
        #endregion

        public bool Initialize()
        {
            var executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var ffmpegFilePath = Path.Combine(executablePath, FFMPEG_FILE_NAME);

            if (!File.Exists(ffmpegFilePath))
            {
                File.WriteAllBytes(ffmpegFilePath, Resources.ffmpeg);
            }

            var audioSnifferFilePath = Path.Combine(executablePath, AUDIO_SNIFFER_FILE_NAME);
            if(!File.Exists(audioSnifferFilePath))
            {
                File.WriteAllBytes(audioSnifferFilePath, Resources.audio_sniffer);
            }

            var scrFilePath = Path.Combine(executablePath, SCREEN_CAPTURE_RECORDER_FILE_NAME);
            if(!File.Exists(scrFilePath))
            {
                File.WriteAllBytes(scrFilePath, Resources.screen_capture_recorder);
            }

            return IsRequireFile();
        }

        /// <summary>
        /// Call after initialization.
        /// </summary>
        public void Register()
        {
            var executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var registerPath = Path.Combine(executablePath, LIBRARY_REGISTER_FILE_NAME);

            var audioSnifferFilePath = Path.Combine(executablePath, AUDIO_SNIFFER_FILE_NAME);
            var scrFilePath = Path.Combine(executablePath, SCREEN_CAPTURE_RECORDER_FILE_NAME);

            if (!File.Exists(registerPath))
            {
                string contents = Resources.library_register
                    .Replace("@mode", "/s")
                    .Replace("@audio_sniffer_file", audioSnifferFilePath)
                    .Replace("@screen_capture_recorder_file", scrFilePath);
                File.WriteAllText(registerPath, contents);

                //process

                File.Delete(registerPath);
            }
        }

        /// <summary>
        /// Call before dispose.
        /// </summary>
        public void UnRegister()
        {
            var executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var registerPath = Path.Combine(executablePath, LIBRARY_REGISTER_FILE_NAME);

            var audioSnifferFilePath = Path.Combine(executablePath, AUDIO_SNIFFER_FILE_NAME);
            var scrFilePath = Path.Combine(executablePath, SCREEN_CAPTURE_RECORDER_FILE_NAME);

            if (!File.Exists(registerPath))
            {
                string contents = Resources.library_register
                    .Replace("@mode", "/u /s")
                    .Replace("@audio_sniffer_file", audioSnifferFilePath)
                    .Replace("@screen_capture_recorder_file", scrFilePath);
                File.WriteAllText(registerPath, contents);

                //process 

                File.Delete(registerPath);
            }
        }

        /// <summary>
        /// Check required files.
        /// </summary>
        /// <returns></returns>
        public bool IsRequireFile()
        {
            var executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var ffmpegFilePath = Path.Combine(executablePath, FFMPEG_FILE_NAME);

            if (!File.Exists(ffmpegFilePath))
            {
                return false;
            }

            var audioSnifferFilePath = Path.Combine(executablePath, AUDIO_SNIFFER_FILE_NAME);
            if (!File.Exists(audioSnifferFilePath))
            {
                return false;
            }

            var scrFilePath = Path.Combine(executablePath, SCREEN_CAPTURE_RECORDER_FILE_NAME);
            if (!File.Exists(scrFilePath))
            {
                return false;
            }

            return true;
        }

        #region events
        private void _frameDropChecker_FrameDroped(object sender, EventArgs e)
        {
            FrameDroped(this, new EventArgs());
        }

        private void cmdHelper_CommandDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {                
                if (e.Data.ToUpper().Contains(DROP_KEYWORD))
                {
                    this._frameDropChecker.FrameDropCount++;
                }
            }
            OnDataReceived(this, e);
        }
        #endregion

        #region functions
        public void FFmpegCommandExcute(string workingDir, string exeFileName, string arguments, bool createNoWindow)
        {
            if (IsRequireFile())
            {
                this._cmdHelper.ExecuteCommand(workingDir, exeFileName, arguments, createNoWindow);
                this._frameDropChecker.FrameDropCheckStart();
            }
        }

        /// <summary>
        /// ffmpeg recording stop.
        /// </summary>        
        public void FFmpegCommandStop()
        {
            this._cmdHelper.CommandLineStandardInput(STOP_COMMAND);
            this._frameDropChecker.FrameDropCheckStop();

            System.Diagnostics.Process[] procs =
                System.Diagnostics.Process.GetProcessesByName(FFMPEG_PROCESS_NAME);

            foreach (var item in procs)
            {
                item.StandardInput.Write(STOP_COMMAND);
                item.WaitForExit();
            }
        }

        /// <summary>
        /// ffmpeg command process force stop.
        /// (process killed.)
        /// </summary>
        public void FFmpegForceStop()
        {
            this._cmdHelper.CommandLineStop();
            this._frameDropChecker.FrameDropCheckStop();
        }

        public bool IsProcessHasExited()
        {
            return this._cmdHelper.RunProcess.HasExited;
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            //memory free is here
            if (disposing)
            {
                if (_cmdHelper != null)
                {
                    _cmdHelper.CommandDataReceived -= cmdHelper_CommandDataReceived;
                    _cmdHelper.Dispose();
                    _cmdHelper = null;
                }

                if (_frameDropChecker != null)
                {
                    _frameDropChecker.FrameDroped -= _frameDropChecker_FrameDroped;
                    _frameDropChecker.Dispose();
                    _frameDropChecker = null;
                }
            }

            disposed = true;
        }
        #endregion
    }

    public class BuildCommand {
        public static string BuildRecordingCommand(RecordingTypes rType, FFmpegCommandModel model)
        {
            switch (rType)
            {
                case RecordingTypes.Local:
                    return BuildRecordingCommandForLocal(model);
                case RecordingTypes.TwitchTV:
                    return BuildRecordingCommandForTwitchTV(model);
                case RecordingTypes.YouTube:
                    throw new Exception("This type was not implemented.");
                default:
                    throw new Exception("Unknown type.");
            }
        }

        /// <summary>
        /// making ffmpeg command string
        /// </summary>
        /// <param name="model"></param>
        /// <returns>builded ffmpeg command string</returns>
        private static string BuildRecordingCommandForLocal(FFmpegCommandModel model)
        {
            string command = CommandConst.GET_DESKTOP_RECODING_COMMAND();
            command = command.Replace("@videoSource", model.VideoSource);
            command = command.Replace("@audioSource", model.AudioSource);
            command = command.Replace("@x", model.OffsetX);
            command = command.Replace("@y", model.OffsetY);
            command = command.Replace("@width", model.Width);
            command = command.Replace("@height", model.Height);
            command = command.Replace("@framerate", model.FrameRate);
            command = command.Replace("@preset", model.Preset);
            command = command.Replace("@audioRate", model.AudioQuality);
            command = command.Replace("@format", model.Format);
            command = command.Replace("@option1", model.Option1);
            command = command.Replace("@filename", model.FullFileName);
            command = command.Replace("@outputquality", model.OutPutQuality);
            return command;
        }

        /// <summary>
        /// not support yet.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>builded ffmpeg command string</returns>
        private static string BuildRecordingCommandForTwitchTV(FFmpegCommandModel model)
        {
            string command = CommandConst.GET_TWITCH_LIVE_COMMNAD();
            command = command.Replace("@videoSource", model.VideoSource);
            command = command.Replace("@audioSource", model.AudioSource);
            command = command.Replace("@x", model.OffsetX);
            command = command.Replace("@y", model.OffsetY);
            command = command.Replace("@width", model.Width);
            command = command.Replace("@height", model.Height);
            command = command.Replace("@framerate", model.FrameRate);
            command = command.Replace("@preset", model.Preset);
            command = command.Replace("@audioRate", model.AudioQuality);
            command = command.Replace("@format", model.Format);
            command = command.Replace("@option1", model.Option1);
            command = command.Replace("@liveUrl", model.FullFileName);
            command = command.Replace("@outputquality", model.OutPutQuality);
            return command;
        }
    }
}
