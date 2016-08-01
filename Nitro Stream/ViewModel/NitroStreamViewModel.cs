﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Security.Permissions;

namespace Nitro_Stream.ViewModel
{
    class NitroStreamViewModel : ViewModelBase
    {
        Model.NtrClient _ntrClient;
        System.Timers.Timer _disconnectTimeout;

        public string configPath { get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.xml"); } }

        bool _connected;

        private StringBuilder _RunningLog;
        public string runningLog
        {
            get { return _RunningLog.ToString(); }
        }

        Model.ViewSettings _ViewSettings;
        public Model.ViewSettings ViewSettings { get { return _ViewSettings; } set { _ViewSettings = value; } }

        public NitroStreamViewModel()
        {
            _ViewSettings = new Model.ViewSettings(true);
            _ntrClient = new Model.NtrClient();
            _disconnectTimeout = new System.Timers.Timer(10000);
            _disconnectTimeout.Elapsed += _disconnectTimeout_Elapsed;
            if (System.IO.File.Exists(configPath))
                _ViewSettings = Model.ViewSettings.Load(configPath);

            _ntrClient.onLogArrival += WriteToLog;
            _ntrClient.Connected += _ntrClient_Connected;
            AppDomain.CurrentDomain.UnhandledException += ExceptionToLog;

            _RunningLog = new StringBuilder("");
        }

        private void _ntrClient_Connected(bool Connected)
        {
            if (Connected)
            {
                byte[] bytes = { 0x70, 0x47 };
                _WriteToDeviceMemory(0x0105AE4, bytes, 0x1a);
                uint pm = (uint)(_ViewSettings.PriorityMode ? 1 : 0);                
                remoteplay(pm, _ViewSettings.PriorityFactor, _ViewSettings.PictureQuality, _ViewSettings.QosValue);
                _disconnectTimeout.Start();

                if (System.IO.File.Exists(_ViewSettings.ViewerPath))
                {
                    StringBuilder args = new StringBuilder();

                    args.Append("-l ");
                    args.Append((_ViewSettings.ViewMode == Model.Orientations.Vertical) ? "0 " : "1 ");
                    args.Append("-t " + _ViewSettings.TopScale.ToString() + " ");
                    args.Append("-b " + _ViewSettings.BottomScale.ToString());

                    System.Diagnostics.ProcessStartInfo p = new System.Diagnostics.ProcessStartInfo(_ViewSettings.ViewerPath);
                    p.Verb = "runas";
                    p.Arguments = args.ToString();
                    Process.Start(p);
                }
                else
                    WriteToLog("NTRViewer not found, please run this manually as admin");
            }
        }

        private void ExceptionToLog(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                throw ex;
            }
            WriteToLog("ERR:" + ex.Message.ToString());
        }

        public void WriteToLog(string msg)
        {
            _RunningLog.Append(msg);
            _RunningLog.Append("\n");
            OnPropertyChanged("RunningLog");
        }

        private void _disconnectTimeout_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            disconnect();
            _disconnectTimeout.Stop();
        }

        public void InitiateRemotePlay()
        {
            if (_connected == false)
            {
                connect(_ViewSettings.IPAddress);
            }
            else
                _ntrClient_Connected(_connected);
        }

        public void connect(string host)
        {
            _ntrClient.setServer(host, 8000);
            _ntrClient.connectToServer();
        }

        public void disconnect()
        {
            _ntrClient.disconnect();
        }

        private void _WriteToDeviceMemory(uint addr, byte[] buf, int pid = -1)
        {
            _ntrClient.sendWriteMemPacket(addr, (uint)pid, buf);
        }

        public void remoteplay(uint priorityMode = 0, uint priorityFactor = 5, uint quality = 90, double qosValue = 15)
        {
            uint num = 1;
            if (priorityMode == 1)
            {
                num = 0;
            }
            uint qosval = (uint)(qosValue * 1024 * 1024 / 8);
            _ntrClient.sendEmptyPacket(901, num << 8 | priorityFactor, quality, qosval);
            WriteToLog("OK: Remoteplay initiated. This client will disconnect in 10 seconds.");
        }

    }
}