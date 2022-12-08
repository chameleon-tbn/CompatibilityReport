using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ColossalFramework;
using CompatibilityReport.Settings;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

namespace CompatibilityReport.Util
{
    public static class LogsCollector {
        public static void CollectLogs() {
            int backup = ZipConstants.DefaultCodePage;
            ZipConstants.DefaultCodePage = Encoding.ASCII.CodePage;
            string timestamp = $"{DateTime.Now:yyyy-MM-dd_hh_mm_ss}";
            string fileName = $"CompatibilityReport_Logs_{timestamp}.zip";
            using (ZipFile zip = ZipFile.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName))) {
                AddGameLog(zip);
                AddFile(zip, GlobalConfig.Instance.GeneralConfig.ReportPath, "CompatibilityReport.html");
                TryIncludeLastErrorLog(zip);
                TryIncludeLastLSMReport(zip);
            }
            ZipConstants.DefaultCodePage = backup;

            Utils.OpenInFileBrowser(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName));
        }
        
        private static void AddFile(ZipFile zip, string directoryPath, string filename, string directoryName= null) {
            if (File.Exists(Path.Combine(directoryPath, filename))) {
                zip.BeginUpdate();
                if (!string.IsNullOrEmpty(directoryName)) {
                    zip.Add(Path.Combine(directoryPath, filename), $"{directoryName}/{filename}");
                } else {
                    zip.Add(Path.Combine(directoryPath, filename), filename);
                }
                zip.CommitUpdate();
            }
        }

        private static void TryIncludeLastErrorLog(ZipFile zip) {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    DirectoryInfo mainGameDir = Directory.GetParent(Application.dataPath);
                    string[] directories = Directory.GetDirectories(mainGameDir.ToString(), $"{DateTime.Now:yyyy-MM-}*");
                    string latest = directories.OrderByDescending(s => s).FirstOrDefault();
                    Logger.Log($"Found CrashDump folder in: {latest}");
                    if (!string.IsNullOrEmpty(latest))
                    {
                        string directoryName = Path.GetDirectoryName(latest);
                        string[] files = Directory.GetFiles(latest);
                        for (int i = 0; i < files.Length; i++)
                        {
                            string name = Path.GetFileName(files[i]);
                            AddFile(zip, latest, name, $"CrashDump_{directoryName}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e);
            }
        }

        private static void TryIncludeLastLSMReport(ZipFile zip) {
            try
            {
                var lsmSettings = Type.GetType("LoadingScreenModRevisited.LSMRSettings, LoadingScreenModRevisited", false);
                if (lsmSettings != null)
                {
                    string path = lsmSettings.GetProperty("ReportDirectory", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null) as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        string[] reports = Directory.GetFiles(path, "*Assets Report*.htm");
                        string lastReport = reports.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                        if (!string.IsNullOrEmpty(lastReport))
                        {
                            string reportName = Path.GetFileName(lastReport);
                            string directoryPath = Path.GetDirectoryName(lastReport);
                            Logger.Log($"LSM Report path: {directoryPath}, file: {reportName}");
                            AddFile(zip, directoryPath, reportName, "LSM_Report");
                        }
                    }
                }
                else
                {
                    Logger.Log("LSM Mod not found, loading report collection skipped.");
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e);
            }
        }
        
        private static void AddGameLog(ZipFile zip) {
            string fileName = Application.platform == RuntimePlatform.WindowsPlayer ? "output_log.txt" : "Player.log";
            // persistentDataPath is valid path for Player.log for Linux, hopefully for MacOSX also...
            string gameLogPath = Application.platform == RuntimePlatform.WindowsPlayer
                                        ? Application.dataPath
                                        : Application.platform != RuntimePlatform.OSXPlayer
                                            ? Application.persistentDataPath
                                            : Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library"), "Logs"), "Unity");

            try {
                string gameLogFilepath = Path.Combine(gameLogPath, fileName);
                if (File.Exists(gameLogFilepath)) {
                    Logger.Log($"Game log path exists: {gameLogFilepath}");
                    //copying file to prevent IOException: Sharing violation on path...
                    File.Copy(gameLogFilepath, Path.Combine(gameLogPath, $"{fileName}.bak"));
                    if (File.Exists(Path.Combine(gameLogPath, $"{fileName}.bak"))) {
                        zip.BeginUpdate();
                        zip.Add(Path.Combine(gameLogPath, $"{fileName}.bak"), fileName);
                        zip.CommitUpdate();
                        File.Delete(Path.Combine(gameLogPath, $"{fileName}.bak"));
                    }
                } else {
                    Logger.Log($"Game log path is incorrect: {gameLogFilepath} Didn't collect log", Logger.LogLevel.Warning);
                }
            } catch (Exception e) {
                Logger.Log(e.ToString(), Logger.LogLevel.Error);
            }
        }
    }
}
