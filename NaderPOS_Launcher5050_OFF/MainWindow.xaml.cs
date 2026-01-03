using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace NaderPOSLauncher
{
  public partial class MainWindow : Window
  {
    private Process? _server;
    private const int Port = 5050;
    private readonly string _url = $"http://127.0.0.1:{Port}/";
    private string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NaderPOS");
    private string LauncherLog => Path.Combine(DataDir, "launcher.log");
    private string ServerLog => Path.Combine(DataDir, "server.log");

    public MainWindow()
    {
      InitializeComponent();
      Directory.CreateDirectory(DataDir);
      Loaded += async (_, __) => await StartAsync();
      Closing += (_, __) => StopServer();
    }

    private void Log(string msg)
    {
      try { File.AppendAllText(LauncherLog, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {msg}\r\n"); } catch { }
    }

    private async Task StartAsync()
    {
      try
      {
        Log("Launcher start");

        // اقفل أي شيء ماسك 5050 قبل ما نشغل السيرفر
        try
        {
          var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort 5050 -ErrorAction SilentlyContinue | Select -Expand OwningProcess -Unique | % { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }\"")
          { UseShellExecute=false, CreateNoWindow=true };
          Process.Start(psi)?.WaitForExit();
        } catch { }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var serverExe = Path.Combine(baseDir, "NaderProductsApp.exe");
        if (!File.Exists(serverExe))
        {
          Log("Missing server exe: " + serverExe);
          MessageBox.Show("ملف السيرفر NaderProductsApp.exe غير موجود بجانب اللانشر", "NADER POS");
          Close();
          return;
        }

        _server = new Process();
        _server.StartInfo.FileName = serverExe;
        _server.StartInfo.UseShellExecute = false;
        _server.StartInfo.CreateNoWindow = true;
        _server.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

        _server.StartInfo.RedirectStandardOutput = true;
        _server.StartInfo.RedirectStandardError = true;

        _server.StartInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{Port}";

        _server.OutputDataReceived += (_, e) => { if(e.Data!=null) try{ File.AppendAllText(ServerLog, e.Data+"\r\n"); } catch{} };
        _server.ErrorDataReceived  += (_, e) => { if(e.Data!=null) try{ File.AppendAllText(ServerLog, "ERR: "+e.Data+"\r\n"); } catch{} };

        _server.Start();
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();

        Log("Server started on 5050");

        var ok = await WaitUntilReady(_url, 35);
        Log("WaitUntilReady=" + ok);
        if(!ok)
        {
          MessageBox.Show("السيرفر ما استجاب. افتح اللوق:\nC:\\ProgramData\\NaderPOS\\server.log", "NADER POS");
          Close();
          return;
        }

        var userData = Path.Combine(DataDir, "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, userData);
        await Web.EnsureCoreWebView2Async(env);

        Web.CoreWebView2.NavigationCompleted += (_, e) => Log($"NavCompleted Success={e.IsSuccess} Status={e.WebErrorStatus}");
        Web.CoreWebView2.Navigate(_url + "index.html");
      }
      catch(Exception ex)
      {
        Log("FATAL: " + ex);
        MessageBox.Show("صار خطأ. افتح:\nC:\\ProgramData\\NaderPOS\\launcher.log", "NADER POS");
        Close();
      }
    }

    private static async Task<bool> WaitUntilReady(string url, int seconds)
    {
      using var http = new HttpClient();
      var end = DateTime.UtcNow.AddSeconds(seconds);
      while(DateTime.UtcNow < end)
      {
        try
        {
          using var r = await http.GetAsync(url);
          if((int)r.StatusCode >= 200 && (int)r.StatusCode < 500) return true;
        }
        catch { }
        await Task.Delay(400);
      }
      return false;
    }

    private void StopServer()
    {
      try { if(_server!=null && !_server.HasExited) _server.Kill(true); Log("Server killed"); } catch { }
    }
  }
}


