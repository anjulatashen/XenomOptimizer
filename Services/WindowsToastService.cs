using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XenomOptimizer.Services;

/// <summary>
/// Dispatches native Windows 10/11 Action Center Toast Notifications
/// with the authentic Xenom Logo replacing the generic system 'i' icon.
/// </summary>
public static class WindowsToastService
{
    public static void ShowToast(string title, string message)
    {
        Task.Run(() =>
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "Assets", "logo.png"),
                    Path.Combine(baseDir, "logo.png"),
                    @"D:\Shop\Xenom Lag Fix\Assets\logo.png"
                };

                string logoPath = candidates.FirstOrDefault(File.Exists) ?? @"D:\Shop\Xenom Lag Fix\Assets\logo.png";

                var cleanTitle = (title ?? "LAG Killer by XENOM").Replace("'", "''").Replace("\"", "`\"");
                var cleanMsg = (message ?? "").Replace("'", "''").Replace("\"", "`\"");

                var script = $@"
$xml = @'
<toast>
    <visual>
        <binding template=""ToastGeneric"">
            <image placement=""appLogoOverride"" hint-crop=""circle"" src=""{logoPath}""/>
            <text>{cleanTitle}</text>
            <text>{cleanMsg}</text>
        </binding>
    </visual>
</toast>
'@
$doc = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]::new()
$doc.LoadXml($xml)
$toast = [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]::new($doc)
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]::CreateToastNotifier(""XenomOptimizer"").Show($toast)
";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{script.Replace("\r\n", " ").Replace("\n", " ")}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsToastService] Failed: {ex.Message}");
            }
        });
    }
}
