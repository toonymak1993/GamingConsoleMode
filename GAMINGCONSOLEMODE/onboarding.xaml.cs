using Microsoft.UI.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.UI;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class onboarding : Page
    {
        private int currentStepIndex = 0;
        private List<OnboardingStep> Steps;
        private Grid OverlayPanel;
        private Action OverlayAction;

        public onboarding()
        {
            this.InitializeComponent();
            AppSettings.Save("onboarding", true);
            InitializeSteps();
            LoadStep();
            CreateOverlayPanel();
        }

        private void InitializeSteps()
        {
            Steps = new List<OnboardingStep>
            {
                new OnboardingStep
                {
                    Title = "Welcome to GCM",
                    Description = "GCM is a modular program designed for gaming...",
                    ImagePath = "ms-appx:///Assets/logo_gcm.png",
                    InfoText = "GCM replaces the traditional desktop shell with a performance-focused gaming interface. You can still access all Windows features, but everything is streamlined.",
                    InfoActionText = "Open Settings",
                    InfoActionCallback = () => Process.Start(new ProcessStartInfo { FileName = "ms-settings:typing", UseShellExecute = true }),
                    ShowSecondaryActionButton = true,
                    SecondaryActionButtonText = "Open Settings Directly",
                    SecondaryActionButtonCallback = () => Process.Start(new ProcessStartInfo { FileName = "ms-settings:typing", UseShellExecute = true }),
                    ShowInfoButton = true
                },
                new OnboardingStep
                {
                    Title = "Keyboard problems in GCM mode",
                    Description = "Too many keyboards for Windows handhelds",
                    ImagePath = "ms-appx:///Assets/logo_gcm.png",
                    InfoText = "GCM replaces the traditional desktop shell with a performance-focused gaming interface. You can still access all Windows features, but everything is streamlined.",
                    InfoActionText = "Open Settings",
                    InfoActionCallback = () => Process.Start(new ProcessStartInfo { FileName = "ms-settings:typing", UseShellExecute = true }),
                    ShowSecondaryActionButton = false,
                    SecondaryActionButtonText = "Open Settings Directly",
                    SecondaryActionButtonCallback = () => Process.Start(new ProcessStartInfo { FileName = "ms-settings:typing", UseShellExecute = true }),
                    ShowInfoButton = true
                },
            };
        }

        private void LoadStep()
        {
            var step = Steps[currentStepIndex];

            HeaderTextBlock.Text = step.Title;
            DescriptionTextBlock.Text = step.Description;
            OnboardingImage.Source = new BitmapImage(new Uri(step.ImagePath));

            BackButton.IsEnabled = currentStepIndex > 0;
            NextButton.Content = currentStepIndex < Steps.Count - 1 ? "Next" : "Finish";

            CustomContentArea.Children.Clear();

            if (step.ShowActionButton && step.ActionButtonCallback != null)
            {
                Button actionButton = new Button
                {
                    Content = step.ActionButtonText ?? "Action",
                    Width = 250,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                actionButton.Click += (s, e) => step.ActionButtonCallback.Invoke();
                CustomContentArea.Children.Add(actionButton);
            }

            if (step.ShowSecondaryActionButton && step.SecondaryActionButtonCallback != null)
            {
                Button secondaryButton = new Button
                {
                    Content = step.SecondaryActionButtonText ?? "More...",
                    Width = 250,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                secondaryButton.Click += (s, e) => step.SecondaryActionButtonCallback.Invoke();
                CustomContentArea.Children.Add(secondaryButton);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentStepIndex < Steps.Count - 1)
            {
                currentStepIndex++;
                LoadStep();
            }
            else
            {
                Frame.Navigate(typeof(startup));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentStepIndex > 0)
            {
                currentStepIndex--;
                LoadStep();
            }
        }

        private void OnboardingImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var step = Steps[currentStepIndex];
            if (!string.IsNullOrEmpty(step.ImagePath))
            {
                try
                {
                    var localPath = step.ImagePath.Replace("ms-appx:///,", "Assets/");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = localPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to open image: " + ex.Message);
                }
            }
        }

        private void CreateOverlayPanel()
        {
            OverlayPanel = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)),
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Colors.Gray),
                MaxWidth = 700,
                MaxHeight = 600,
                MinHeight = 300,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Colors.DarkGray),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Spacing = 10 };
            var title = new TextBlock
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White)
            };
            var contentScroll = new ScrollViewer
            {
                MaxHeight = 340,
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Name = "OverlayDescriptionText",
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };

            var actionButton = new Button
            {
                Content = "",
                Width = 200,
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };
            actionButton.Click += (s, e) => OverlayAction?.Invoke();

            var exitButton = new Button
            {
                Content = "✕",
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 36,
                Height = 36,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Colors.White)
            };
            exitButton.Click += (s, e) => OverlayPanel.Visibility = Visibility.Collapsed;

            stack.Children.Add(exitButton);
            stack.Children.Add(title);
            stack.Children.Add(contentScroll);
            stack.Children.Add(actionButton);

            border.Child = stack;
            OverlayPanel.Children.Add(border);

            (this.Content as Panel)?.Children.Add(OverlayPanel);
        }

        private void ShowOverlayPanel(string titleText, string description, string buttonText, Action buttonAction)
        {
            if (OverlayPanel != null)
            {
                var stack = (OverlayPanel.Children[0] as Border)?.Child as StackPanel;
                if (stack != null)
                {
                    ((TextBlock)stack.Children[1]).Text = titleText;
                    ((stack.Children[2] as ScrollViewer).Content as TextBlock).Text = description;

                    var btn = (Button)stack.Children[3];
                    if (!string.IsNullOrEmpty(buttonText) && buttonAction != null)
                    {
                        btn.Content = buttonText;
                        btn.Visibility = Visibility.Visible;
                        OverlayAction = buttonAction;
                    }
                    else
                    {
                        btn.Visibility = Visibility.Collapsed;
                        OverlayAction = null;
                    }
                }
                OverlayPanel.Visibility = Visibility.Visible;
            }
        }
    }

    public class OnboardingStep
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public string InfoText { get; set; }
        public string InfoActionText { get; set; }
        public Action InfoActionCallback { get; set; }
        public bool ShowActionButton { get; set; } = false;
        public string ActionButtonText { get; set; }
        public Action ActionButtonCallback { get; set; }
        public bool ShowSecondaryActionButton { get; set; } = false;
        public string SecondaryActionButtonText { get; set; }
        public Action SecondaryActionButtonCallback { get; set; }
        public bool ShowInfoButton { get; set; } = false;
    }
}
