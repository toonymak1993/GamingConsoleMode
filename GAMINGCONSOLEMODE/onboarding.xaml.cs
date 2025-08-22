using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using Microsoft.UI.Text;
using Windows.UI;

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
                    SlideContainer.Children.Add(BuildSlide_4());
                    break;
                case 2:
                    SlideContainer.Children.Add(BuildSlide_5());
                    break;
                case 3:
                    SlideContainer.Children.Add(BuildSlide_6());
                    break;
                case 4:
                    SlideContainer.Children.Add(BuildSlide_2());
                    break;
                case 5:
                    SlideContainer.Children.Add(BuildSlide_1());
                    break;
                case 6:
                    SlideContainer.Children.Add(BuildSlide_allygamepad());
                    break;
                case 7:
                    SlideContainer.Children.Add(BuildSlide_3());
                    break;
                case 8:
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

        private UIElement BuildSlide_4()
        {
            // 1. Erstelle ein Haupt-StackPanel, das alles untereinander anordnet.
            var mainStack = new StackPanel
            {
                Spacing = 20,
                Padding = new Thickness(20),
                MaxWidth = 800, // Sorgt für gute Lesbarkeit auf breiten Bildschirmen
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 2. Füge einen Titel hinzu (konsistent mit den anderen Seiten)
            mainStack.Children.Add(new TextBlock
            {
                Text = "Autostart Management",
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // 3. Dein Code für den ToggleSwitch-Kasten (leicht angepasst)
            var toggleSwitch = new ToggleSwitch
            {
                Name = "UsewinpartStartapps",
                Header = "Disable autostart apps",
                OnContent = "On",
                OffContent = "Off"
            };
            toggleSwitch.Toggled += UsewinpartStartapps_Toggled;

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1)
            };
            gradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 45, 45, 48), Offset = 0.0 });
            gradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 62, 62, 66), Offset = 1.0 });

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 10, 15, 10), // Etwas mehr Padding
                HorizontalAlignment = HorizontalAlignment.Center, // Zentriert den Kasten
                Background = gradientBrush,
                Child = toggleSwitch
            };

            // Füge den Kasten zum Haupt-StackPanel hinzu
            mainStack.Children.Add(border);

            // 4. Füge den neuen Erklärungstext unter dem Kasten hinzu
            mainStack.Children.Add(new TextBlock
            {
                Text = "You can choose to let GCM disable the default autostart applications to ensure you're not interrupted. Alternatively, you can manage this yourself by making sure no apps pop up that might interfere with GCM. This setting is optional.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.LightGray), // Etwas unauffälliger als Weiß
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });

            // 5. Gib das gesamte StackPanel als UI-Element zurück
            return mainStack;
        }

        private UIElement BuildSlide_1()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "Handheld - Fix On-Screen Keyboard popup",
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
        //shortcut GCM Overlay
        private UIElement BuildSlide_2()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "GCM Overlay",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Responsive Rounded Image in Viewbox
            var imageContainer = new Viewbox
            {
                MaxHeight = 450,
                MaxWidth = 800,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(15),

                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/gcmoverlay.gif")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);


           

            stack.Children.Add(new TextBlock
            {
                Text = "You can use the shown key combination on your controller or handheld to display the GCM overlay.\r\nCurrently, it can only list your shortcuts, but it will be expanded in the future.\r\nYou can adjust the combination at any time in the Shortcuts settings.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });


          

            return stack;
        }
        //shortcut GCM Taskmanager
        private UIElement BuildSlide_5()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "GCM Taskmanager",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Responsive Rounded Image in Viewbox
            var imageContainer = new Viewbox
            {
                MaxHeight = 450,
                MaxWidth = 800,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(15),

                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/taskmanager.gif")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);




            stack.Children.Add(new TextBlock
            {
                Text = "GCM includes a task manager that allows you to switch between apps, as well as jump to the launcher, Discord, and more.\r\nPlease note: If the launcher is not visible, it has automatically minimized itself. Simply use the shortcut to bring it back.\r\nYou can also open the task manager at any time, even while playing.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });




            return stack;
        }
        //mousecontroll
        private UIElement BuildSlide_6()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "Mouse Control",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Responsive Rounded Image in Viewbox
            var imageContainer = new Viewbox
            {
                MaxHeight = 450,
                MaxWidth = 800,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(15),

                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/mousecontroll.gif")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);




            stack.Children.Add(new TextBlock
            {
                Text = "If you use Steam, you have the option to use the Steam Guide Control.\r\nYou can find the settings for this under Steam → Controller. This feature allows you to control your mouse with your controller and also open the Steam keyboard.\r\nIf you use something else, you can also use JoyXoff, which is supported by GCM.\r\nOn your handheld, this is mostly irrelevant, as most side panels have built-in mouse control, or you can simply use touch input.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });




            return stack;
        }
        //rog ally gamepad modus
        private UIElement BuildSlide_allygamepad()
        {
            var stack = new StackPanel { Spacing = 20, Padding = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "Handheld - Gamepad Mode",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });

            // Responsive Rounded Image in Viewbox
            var imageContainer = new Viewbox
            {
                MaxHeight = 450,
                MaxWidth = 800,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(15),

                Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/onboarding/allygamepad.jpg")),
                    Stretch = Stretch.Uniform
                }
            };

            imageContainer.Child = imageBorder;
            stack.Children.Add(imageContainer);




            stack.Children.Add(new TextBlock
            {
                Text = "Always set your ROG Ally, or any handheld, to Gamepad Mode.\r\nGCM does not work with keyboards, but primarily with Xbox controllers.\r\nYour handheld mainly uses the XInput API, which tells the system it is an Xbox controller, making the entire controller mapping standard across most handheld devices.",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center
            });




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

        #region methodes for onboarding
        private void UsewinpartStartapps_Toggled(object sender, RoutedEventArgs e)
        {
            // Wir prüfen, ob der "sender" tatsächlich ein ToggleSwitch ist, 
            // und erstellen dabei direkt eine benannte Variable dafür.
            if (sender is ToggleSwitch toggleSwitch)
            {
                // Jetzt können wir die "IsOn"-Eigenschaft abfragen, die true oder false ist.
                if (toggleSwitch.IsOn)
                {
                    AppSettings.Save("usewinpartstartapps", true);
                    Debug.WriteLine("Einstellung 'usewinpartstartapps' wurde auf AN gesetzt.");
                }
                else
                {
                    AppSettings.Save("usewinpartstartapps", false);
                    Debug.WriteLine("Einstellung 'usewinpartstartapps' wurde auf AUS gesetzt.");
                }
            }
        }

        #endregion methodes for onboarding
    }
}
