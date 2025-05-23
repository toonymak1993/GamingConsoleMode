using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.IO;

namespace GAMINGCONSOLEMODE
{



    // Data model for a single onboarding step
    public class OnboardingStep
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
        public bool ShowActionButton { get; set; } = false;
        public string ActionButtonText { get; set; }
        public Action ActionButtonCallback { get; set; }
    }

    public sealed partial class onboarding : Page
    {
        #region need variable
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        #endregion need variable

        private int currentStepIndex = 0;
        private List<OnboardingStep> Steps;

        public onboarding()
        {
            this.InitializeComponent();
            AppSettings.Save("onboarding", true);
            InitializeSteps();
            LoadStep();
        }

        // Define all onboarding steps here
        private void InitializeSteps()
        {
            Steps = new List<OnboardingStep>
            {
                //welcome
                new OnboardingStep
                {
                    Title = "Welcome to GCM",
                    Description = "GCM is a modular program designed to help you focus your PC entirely on gaming. " +
                    "The main feature of GCM is replacing the Windows desktop with a launcher of your choice. " +
                    "This unlocks several advantages that are especially valuable for gaming. For example, the Steam overlay " +
                    "runs more smoothly without interference from the Windows window manager, you're no longer bothered by pop-ups, " +
                    "and your PC boots up noticeably faster since the launcher becomes your primary interface.",
                    ImagePath = "ms-appx:///Assets/logo_gcm.png"
                },
                //taskmanager
                new OnboardingStep
                {
                    Title = "Taskmanager",
                    Description = "GCM includes a built-in task manager. You can access it by minimizing the launcher or using the GCM shortcuts function" +
                    " It can be operated using either a keyboard or a controller. The purpose of the task manager is to let you easily switch back and forth " +
                    "between your launcher and any game that might start in the background.",
                    ImagePath = "ms-appx:///Assets/onboarding/taskmanager_gcmloader.png",
                    ShowActionButton = false,
                    ActionButtonText = "Start Now",
                    ActionButtonCallback = () =>
                    {
                    }
                },
                //Functions
                new OnboardingStep
                {
                    Title = "Functions",
                    Description = "GCM is an app you can think of as a \"build your own gaming experience\" platform. " +
                    "While it comes with a set of core features like the built-in task manager and customizable shortcuts, it also offers a wide range of useful " +
                    "tools—such as Decky Loader for Steam, startup videos, and preconfigured audio settings. " +
                    "At the end of the onboarding process, you'll see all the available features you can choose from.",
                    ImagePath = "ms-appx:///Assets/onboarding/functions_gcm.png",
                    ShowActionButton = false,
                    ActionButtonText = "Start Now",
                    ActionButtonCallback = () =>
                    {
                    }
                },
                //Shortcuts
                new OnboardingStep
                {
                    Title = "GCM Shortcuts",
                    Description = "GCM Shortcuts offers you a shortcut function, please be sure to create shortcuts to use the task manager as an example and much more",
                    ImagePath = "ms-appx:///Assets/onboarding/shortcuts.png",
                    ShowActionButton = false,
                    ActionButtonText = "Start Now",
                    ActionButtonCallback = () =>
                    {
                    }
                },
                //Discord
                new OnboardingStep
                {
                    Title = "Connect with us on Discord",
                    Description = "We've launched a new Discord server where you can connect with us anytime—share your ideas, " +
                    "report bugs, or just chat. Together, we’re building what Microsoft hasn’t: a true gaming operating system " +
                    "powered by the amazing compatibility of Windows.",

                    ImagePath = "ms-appx:///Assets/onboarding/discord.jpg",
                    ShowActionButton = true,
                    ActionButtonText = "Join GCM Discord Server",
                    ActionButtonCallback = () =>
                    {
                        string discordInvite = "https://discord.gg/FbjYDeEJce";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = discordInvite,
                UseShellExecute = true
            });

            Console.WriteLine("Discord-Server wird geöffnet...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Öffnen des Links: {ex.Message}");
        }
                    }
                }
            };
        }

        // Load the UI for the current step
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
                // Finish the onboarding
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
    }
}
