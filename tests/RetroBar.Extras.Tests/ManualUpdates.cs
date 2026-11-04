using System;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml.Linq;
using RetroBar.Utilities;

// UXUI-Extreme (UX-019): exercise the real settings serializer and updater lifecycle.
// Run only with a clean test profile. These checks do not launch the shell or browse a URL.
internal static class ManualUpdates
{
    public static void Run(Action<bool, string> check)
    {
        // Loading Settings can migrate other old properties and save them immediately.
        // Refuse an existing user file before touching that type; CI uses a fresh profile.
        string existingSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar", "settings.json");
        if (File.Exists(existingSettings))
            throw new InvalidOperationException("Manual-update contracts require a clean test profile; an existing RetroBar settings.json was left untouched. Use a disposable test account or GitHub CI.");

        Type settingsType = typeof(Updater).Assembly.GetType("RetroBar.Utilities.Settings", throwOnError: true);
        PropertyInfo setting = settingsType.GetProperty("CheckForUpdates");
        foreach (string json in new[] { "{}", "{\"CheckForUpdates\":false}", "{\"CheckForUpdates\":true}" })
        {
            object settings = JsonSerializer.Deserialize(json, settingsType);
            check(setting.GetValue(settings) is false, $"update policy for saved settings {json}");
            int notifications = 0;
            ((INotifyPropertyChanged)settings).PropertyChanged += (_, _) => notifications++;
            setting.SetValue(settings, true);
            check(setting.GetValue(settings) is false && notifications == 0, "old callers cannot enable updates or trigger a settings write");
            using var saved = JsonDocument.Parse(JsonSerializer.Serialize(settings, settingsType));
            check(!saved.RootElement.GetProperty("CheckForUpdates").GetBoolean(), "a normal save normalizes the legacy flag to false");
            check(saved.RootElement.GetProperty("Theme").GetString() == "Windows 95-98", "normal settings still serialize alongside the retired flag");
        }
        check(!File.Exists(existingSettings), "settings fixtures do not create a user settings file");

        // Observe the actual HTTP EventSource beyond the old updater's ten-second startup
        // delay. Network requests are counted from RequestStart even if the connection fails.
        using (var requests = new HttpRequests())
        {
            // Prove the observer is active. The handler returns an in-memory response;
            // this positive control cannot resolve a host or create a network connection.
            using (var client = new HttpClient(new InMemoryResponse()))
            using (HttpResponseMessage response = client.GetAsync("https://ux019.invalid/observer-check").GetAwaiter().GetResult())
                check(response.IsSuccessStatusCode && requests.Count > 0, "HTTP observer detects a request through the in-memory handler");
            requests.Reset();

            using var updater = new Updater();
            check(!updater.IsUpdateAvailable, "no unsolicited update prompt at startup");
            check(updater.DownloadUrl == "https://github.com/techrote/RetroBar-Extreme/releases", "manual downloads target this fork");
            Thread.Sleep(TimeSpan.FromSeconds(11));
            check(requests.Count == 0, "updater must not start HTTP requests after the former startup delay");
            check(!updater.IsUpdateAvailable, "remaining idle cannot advertise a stock RetroBar update");
            updater.Dispose();
            updater.Dispose();
        }

        // The existing Windows build compiles the complete Properties window and installer.
        // Check their user-facing contract here without opening a taskbar or a browser.
        string root = FindSourceRoot();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument window = XDocument.Load(Path.Combine(root, "RetroBar", "PropertiesWindow.xaml"));
        XElement link = window.Descendants().Single(e => (string)e.Attribute(x + "Name") == "ManualReleasesLink");
        check(link.Name.LocalName == "Hyperlink", "manual releases remain keyboard-accessible as a hyperlink");
        check((string)link.Attribute("NavigateUri") == "{x:Static Settings:Updater.ReleasesUri}", "Properties uses the typed fork URI from the updater interface");
        check((string)link.Attribute("RequestNavigate") == "Hyperlink_RequestNavigate", "releases open only through the existing user navigation handler");
        check(!window.Descendants().Any(e => e.Name.LocalName == "CheckBox" &&
            e.Attributes().Any(a => a.Value.Contains("CheckForUpdates"))), "no enabled automatic-update switch is presented");
        var words = new ResourceDictionary { Source = new Uri("/RetroBar;component/Controls/UpdateStrings.xaml", UriKind.Relative) };
        check(words["uxui_updates_manual"] is string text && text.Contains("Manual"), "manual-update explanation is compiled into the app");
        check(words["uxui_updates_releases"] as string == "RetroBar-Extreme releases", "release link identifies the fork");

        // Instantiate the actual row, not an imitation. Strip only the event handler (the
        // private PropertiesWindow owns it) and supply the source document's namespaces.
        // This catches x:Static returning a string where WPF requires a System.Uri.
        var notice = new XElement(window.Descendants().Single(e => (string)e.Attribute(x + "Name") == "ManualUpdatesNotice"));
        notice.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        notice.SetAttributeValue(XNamespace.Xmlns + "Settings", "clr-namespace:RetroBar.Utilities;assembly=RetroBar");
        foreach (XAttribute handler in notice.Descendants().Attributes("RequestNavigate").ToArray()) handler.Remove();
        var rendered = (TextBlock)XamlReader.Parse(notice.ToString());
        rendered.Resources.MergedDictionaries.Add(words);
        Hyperlink renderedLink = rendered.Inlines.OfType<Hyperlink>().Single();
        check(renderedLink.NavigateUri.AbsoluteUri == Updater.ReleasesUrl, "the real WPF row loads with the fork URI");
        check(renderedLink.Focusable, "manual release navigation can receive keyboard focus");

        string installer = File.ReadAllText(Path.Combine(root, "installer.iss"));
        check(!installer.Contains("dremin.github.io/updates") && !installer.Contains("CheckForUpdates;"), "installer cannot run the inherited application-update probe");
        check(installer.Contains("AppUpdatesURL={#RetroBarReleasesURL}") && installer.Contains(Updater.ReleasesUrl), "installer's manual updates metadata points to the fork");
    }

    private static string FindSourceRoot()
    {
        for (DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RetroBar.sln"))) return directory.FullName;
        throw new InvalidOperationException("Run Extras contracts from the repository or one of its subdirectories.");
    }

    private sealed class HttpRequests : EventListener
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public void Reset() => Interlocked.Exchange(ref count, 0);

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "System.Net.Http") EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == "RequestStart") Interlocked.Increment(ref count);
        }
    }

    private sealed class InMemoryResponse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
