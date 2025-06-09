using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GAMINGCONSOLEMODE
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class version_news : Window
    {
        public version_news()
        {
            this.InitializeComponent();
            LoadVersionNews();
            
        }

        private void LoadVersionNews()
        {
            var items = new List<VersionNewsItem>
    {
                 new VersionNewsItem
        {
            Title = "Version 2.2.0",
            Date = "2025-06-10",
            ContentItems = new List<NewsContentItem>
{
    // 🧠 UNDER THE HOOD
    new NewsContentItem { Icon = "", Text = "--UNDER THE HOOD--" },
    new NewsContentItem { Icon = "🔁", Text = "GCM is now hybrid: it runs on top of Windows, allowing full compatibility with Game Pass and more." },
    new NewsContentItem { Icon = "🚫", Text = "New feature to disable autostart apps directly from GCM." },

    // 🎮 SHORTCUTS
    new NewsContentItem { Icon = "", Text = "--SHORTCUTS--" },
    new NewsContentItem { Icon = "🔀", Text = "Shortcuts now support Seamless Switch – toggle between modes with a single button press." },
    new NewsContentItem { Icon = "🔔", Text = "Overlay and toast-style popups are now displayed when shortcuts are used." },
    new NewsContentItem { Icon = "⚙️", Text = "Default shortcuts like Task Manager and Show Overlay are now auto-applied if missing." },

    // 📊 TASK MANAGER
    new NewsContentItem { Icon = "", Text = "--TASK MANAGER--" },
    new NewsContentItem { Icon = "🆕", Text = "Redesigned layout with improved responsiveness." },
    new NewsContentItem { Icon = "👀", Text = "Better process tracking: understand at a glance what’s currently running." },
    new NewsContentItem { Icon = "📱", Text = "Handheld Touch Launcher added for quick access to apps like Spotify." },
    new NewsContentItem { Icon = "🕒", Text = "Top bar now shows clock, Wi-Fi status, and settings (touch-only for now)." },

    // 🔐 SECURITY
    new NewsContentItem { Icon = "", Text = "--SECURITY--" },
    new NewsContentItem { Icon = "🛡️", Text = "UAC preference is now respected: GCM won’t reactivate UAC if you disable it manually." },

    // ⬆️ UPDATER
    new NewsContentItem { Icon = "", Text = "--UPDATER--" },
    new NewsContentItem { Icon = "🔧", Text = "Improved internal updater for smoother update handling." },

    // ✨ UI/UX
    new NewsContentItem { Icon = "", Text = "--UI / UX--" },
    new NewsContentItem { Icon = "🧩", Text = "Functions are now called Extensions and have been visually redesigned." },
    new NewsContentItem { Icon = "🎨", Text = "Launcher visuals and user interaction have been improved." }
},


        },
        new VersionNewsItem
        {
            Title = "Version 2.1.1",
            Date = "2025-04-19",
            ContentItems = new List<NewsContentItem>
            {
               new NewsContentItem { Icon = "🔊", Text = "Volume Button functionality added for ROG Ally and MSI Claw." },
new NewsContentItem { Icon = "🎯", Text = "Custom shortcuts can now be defined for all functions. (Note: Task Manager also requires a custom shortcut.)" },
new NewsContentItem { Icon = "⚡", Text = "Task Manager performance significantly improved for smoother usage." },
new NewsContentItem { Icon = "🆕", Text = "Initial detection and landing page for ROG Ally added. (Note: No features available on this page yet.)" },
new NewsContentItem { Icon = "🛠️", Text = "The GCM settings can now be reset under Settings" },
new NewsContentItem { Icon = "🛠️", Text = "General software stability improvements." }

            }
        }
    };

            NewsList.ItemsSource = items;
        }
    }
}
public class VersionNewsItem
{
    public string Title { get; set; }
    public string Date { get; set; }
    public List<NewsContentItem> ContentItems { get; set; }
}

public class NewsContentItem
{
    public string Icon { get; set; } // z. B. "🛠️"
    public string Text { get; set; }
}
