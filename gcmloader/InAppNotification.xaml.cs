using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace gcmloader
{
    public sealed partial class InAppNotification : UserControl
    {
        public event EventHandler AnimationFinished;
        private DispatcherTimer _lifeTimer;

        public InAppNotification(string message)
        {
            this.InitializeComponent();
            MessageText.Text = message;

            // Timer: 3.5 Sekunden sichtbar
            _lifeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            _lifeTimer.Tick += LifeTimer_Tick;

            // Starten sobald geladen
            this.Loaded += (s, e) =>
            {
                EnterStoryboard.Begin();
                _lifeTimer.Start();
            };
        }

        private void LifeTimer_Tick(object sender, object e)
        {
            _lifeTimer.Stop();
            // Raus-Animation
            ExitStoryboard.Begin();
        }

        private void ExitStoryboard_Completed(object sender, object e)
        {
            // Bescheid geben zum Löschen
            AnimationFinished?.Invoke(this, EventArgs.Empty);
        }
    }
}