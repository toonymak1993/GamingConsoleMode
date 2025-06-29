using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using Microsoft.UI.Text;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class onboarding : Page
    {
        private int currentStep = 0;

        public onboarding()
        {
            this.InitializeComponent();
            LoadSlide(currentStep);
        }

        private void LoadSlide(int index)
        {
            SlideContainer.Children.Clear();

            switch (index)
            {
                case 0:
                    SlideContainer.Children.Add(BuildSlide_0());
                    break;
                case 1:
                    SlideContainer.Children.Add(BuildSlide_1());
                    break;
                case 2:
                    SlideContainer.Children.Add(BuildSlide_2());
                    break;
                case 3:
                    SlideContainer.Children.Add(BuildSlide_3());
                    break;
                case 4:
                    Frame.Navigate(typeof(startup)); // oder Finale Seite
                    break;
            }
        }


        private UIElement BuildSlide_0()
        {
            // Root panel that adjusts nicely on all screen sizes
            var stack = new StackPanel
            {
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Title
            stack.Children.Add(new TextBlock
            {
                Text = "Welcome to Gaming Console Mode",
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Image
            stack.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/gcm_ui_logo.png")),
                Height = 200,
                HorizontalAlignment = HorizontalAlignment.Center,
                Stretch = Stretch.Uniform
            });

            // Description
            stack.Children.Add(new TextBlock
            {
                Text = "Welcome to GCM – a better gaming experience awaits you!\r\nPlease take a moment to go through the onboarding, as it covers some important points.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 26,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            return stack;
        }
        private UIElement BuildSlide_1()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "(Handheld) Fix On-Screen Keyboard error",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Responsive Rounded Image in Viewbox
            var imageContainer = new Viewbox
            {
                MaxHeight = 350,
                MaxWidth = 800,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(15),
                
                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/onscreenkeyboard.png")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);


            var button = new Button
            {
                Content = "Open Typing Settings",
                Width = 240,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            button.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:typing") { UseShellExecute = true });
                }
                catch { }
            };

            stack.Children.Add(button);

            stack.Children.Add(new TextBlock
            {
                Text = "When the settings window opens:\n\n• Scroll down to the “On-screen keyboard” section.\n• Click on the dropdown next to “Show the on-screen keyboard”.\n• From the list, select “Never”.\n\nThis will prevent the on-screen keyboard from automatically popping up when you don’t need it.\nIt’s especially useful on handheld devices like the ROG Ally or similar systems.\n\n✅ You can close the settings window once it’s set to “Never”.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            return stack;
        }
        //shortcuts
        private UIElement BuildSlide_2()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "GCM Shortcuts",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Responsive Rounded Image in Viewbox
            var imageContainer = new Viewbox
            {
                MaxHeight = 200,
                MaxWidth = 500,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(15),

                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/shortcuts.png")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);


           

            stack.Children.Add(new TextBlock
            {
                Text = "GCM uses controller shortcuts to trigger actions like the Task Manager, Overlay, Audio Settings, and more.\r\nDefault shortcuts are provided if you haven't configured your own yet.\r\nYou can review or customize them under GCM Shortcuts.\r\n\r\n",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });


            // Responsive Rounded Image in Viewbox
            var imageContainer2 = new Viewbox
            {
                MaxHeight = 200,
                MaxWidth = 500,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder2 = new Border
            {
                CornerRadius = new CornerRadius(15),

                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/shortcutsnorm.png")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer2.Child = imageBorder2;
            stack.Children.Add(imageContainer2);

            return stack;
        }
        //Discord
        private UIElement BuildSlide_3()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            // Titel
            stack.Children.Add(new TextBlock
            {
                Text = "Join Our Discord Community",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Discord Logo oder Banner mit runden Ecken
            var imageContainer = new Viewbox
            {
                MaxHeight = 200,
                MaxWidth = 500,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/discord.jpg")), // <-- dein Bild
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);

            // Beschreibung
            stack.Children.Add(new TextBlock
            {
                Text = "Want to connect with other GCM users, get support, share feedback, or stay updated?\n\nJoin our official Discord server and become part of the community.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Discord Join Button
            var button = new Button
            {
                Content = "Join Discord",
                Width = 240,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            button.Click += (s, e) =>
            {
                try
                {
                    // DEIN EINLADUNGS-LINK HIER EINTRAGEN
                    Process.Start(new ProcessStartInfo("https://discord.gg/FbjYDeEJce") { UseShellExecute = true });
                }
                catch { }
            };

            stack.Children.Add(button);

            return stack;
        }



        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep > 0)
            {
                currentStep--;
                LoadSlide(currentStep);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            currentStep++;
            LoadSlide(currentStep);
        }
    }
}
