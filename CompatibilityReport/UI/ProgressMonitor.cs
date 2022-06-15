using System;
using CompatibilityReport.Settings;
using UnityEngine;
using Settings_1 = Settings;

namespace CompatibilityReport.UI
{
    public class ProgressMonitor : IDisposable
    {
        public DateTime StartedAt { get; private set; }
        public DateTime FinishedAt { get; private set; }

        public event Action<float> eventProgressChanged;
        public event Action eventDisposed;
        public float CurrentProgress => currentStep / (float)numOfSteps;
        public bool Working { get; private set; }

        public bool Completed { get; private set; }

        public bool Aborted { get; private set; }

        public string ProgressMessage
        {
            get { return _progressMessage; }
            private set {
                if (!string.IsNullOrEmpty(value) && ProgressMessage != value)
                {
                    _progressMessage = value;
                }
            }
        }
        
        private string _progressMessage = string.Empty;
        private int numOfSteps = 1;
        private int currentStep = 1;
        private bool reportTime;

        private ProgressMonitorUI ui;
        
        public ProgressMonitor(ProgressMonitorUI ui)
        {
            this.ui = ui;
        }

        public void StartProgress(int stepsCount, string message, bool reportTime = false)
        {
            if (stepsCount == 0)
            {
                throw new ArgumentException($"{nameof(stepsCount)} cannot be 0!");
            }
            numOfSteps = stepsCount;
            currentStep = 0;
            Working = true;
            Completed = false;
            Aborted = false;
            StartedAt = DateTime.Now;
            FinishedAt = DateTime.MinValue;
            ProgressMessage = message;
            if (ui)
            {
                this.reportTime = reportTime;
                if (!reportTime)
                {
                    ui.UpdateElapsedTime(TimeSpan.Zero);
                }
                ui.ProgressTitle = message;
                ui.ProgressText = message;
            }
            if (reportTime)
            {
                ui.UpdateEstimatedTime((double)GlobalConfig.Instance.UpdaterConfig.EstimatedMillisecondsPerModPage * stepsCount);
            }
            else
            {
                ui.UpdateEstimatedTime(0);
            }
        }

        public void UpdateStage(int current, int max)
        {
            if (ui)
            {
                ui.UpdateStage(current, max);
            }
        }

        public void ReportProgress(int step, string message = null)
        {
            ProgressMessage = message;
            currentStep = step;
            if (step == numOfSteps)
            {
                Working = false;
                Completed = true;
                FinishedAt = DateTime.Now;
            }
            
            if (ui)
            {
                ui.Progress = CurrentProgress;
                ui.ProgressText = message;
                if (reportTime)
                {
                    ui.UpdateElapsedTime(DateTime.Now.Subtract(StartedAt));
                }
            }
            eventProgressChanged?.Invoke(CurrentProgress);
        }

        public void PushMessage(string message)
        {
            if (ui)
            {
                ui.PushMessage(message);
            }
        }

        public void Abort()
        {
            if (ui)
            {
                ui.ProgressText = "Abort!";
            }
            if (Working && !Completed)
            {
                Aborted = true;
                Working = false;
                FinishedAt = DateTime.Now;
            }
            eventProgressChanged?.Invoke(1f);
        }

        public void Dispose()
        {
            ui = null;
            eventDisposed?.Invoke();
            eventDisposed = null;
            eventProgressChanged = null;
        }
    }
}
