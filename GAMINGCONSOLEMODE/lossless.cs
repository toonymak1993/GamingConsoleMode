using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace GAMINGCONSOLEMODE // The namespace for your application.
{
    /// <summary>
    /// This class is built to reliably read and write individual settings
    /// for the "Standard" profile in Lossless Scaling's Settings.xml.
    /// It's designed to be future-proof by using LINQ to XML, so it won't
    /// crash even if Lossless Scaling adds new settings to the file.
    /// </summary>
    public static class LosslessSettingsManager
    {
        /// <summary>
        /// Figures out where the Lossless Scaling settings file lives.
        /// </summary>
        private static string GetConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lossless Scaling", "Settings.xml");
        }

        /// <summary>
        /// Finds the "Standard" profile node within the XML document.
        /// </summary>
        private static (XElement?, string?) GetStandardProfileNode(XDocument doc)
        {
            XElement? standardProfile = doc.Root
                ?.Element("GameProfiles")
                ?.Elements("Profile")
                .FirstOrDefault(p => p.Element("Title")?.Value == "Standard");

            if (standardProfile == null)
            {
                return (null, "The 'Standard' profile could not be found in the XML file.");
            }
            return (standardProfile, null);
        }

        /// <summary>
        /// Reads a specific setting's value from the "Standard" profile.
        /// </summary>
        /// <param name="settingName">The exact name of the XML tag (e.g., "DrawFps").</param>
        /// <returns>The setting's value as a string. Returns null if something goes wrong or it's not found.</returns>
        public static string? GetProfileSetting(string settingName)
        {
            try
            {
                string configPath = GetConfigPath();
                if (!File.Exists(configPath)) return null;

                XDocument doc = XDocument.Load(configPath);
                var (standardProfile, error) = GetStandardProfileNode(doc);
                if (error != null) return null;

                XElement? settingElement = standardProfile.Element(settingName);
                return settingElement?.Value;
            }
            catch
            {
                // If anything at all goes wrong during the read, just return null.
                return null;
            }
        }

        /// <summary>
        /// Changes a specific setting in the "Standard" profile.
        /// If the setting doesn't exist, this method will create it.
        /// </summary>
        /// <param name="settingName">The exact name of the XML tag (e.g., "DrawFps").</param>
        /// <param name="value">The new value to write. It will be converted to a string.</param>
        /// <returns>True on success, false on failure.</returns>
        public static bool SetProfileSetting(string settingName, object value)
        {
            try
            {
                string configPath = GetConfigPath();
                if (!File.Exists(configPath)) return false;

                XDocument doc = XDocument.Load(configPath);
                var (standardProfile, error) = GetStandardProfileNode(doc);
                if (error != null)
                {
                    MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                XElement? settingElement = standardProfile.Element(settingName);
                string valueStr = Convert.ToString(value, CultureInfo.InvariantCulture);

                if (settingElement != null)
                {
                    // If the element is already there, just update its value.
                    settingElement.Value = valueStr;
                }
                else
                {
                    // If we can't find the element, we'll create a new one and add it.
                    standardProfile.Add(new XElement(settingName, valueStr));
                }

                doc.Save(configPath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while saving setting '{settingName}':\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        #region Handy Helper Methods for Type Conversions

        /// <summary>
        /// A little helper to get a setting and convert it directly to a boolean.
        /// </summary>
        public static bool GetProfileSettingAsBool(string settingName, bool defaultValue = false)
        {
            string? value = GetProfileSetting(settingName);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        /// <summary>
        /// A little helper to get a setting and convert it directly to an integer.
        /// </summary>
        public static int GetProfileSettingAsInt(string settingName, int defaultValue = 0)
        {
            string? value = GetProfileSetting(settingName);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// A little helper to get a setting and convert it directly to a double.
        /// </summary>
        public static double GetProfileSettingAsDouble(string settingName, double defaultValue = 0.0)
        {
            string? value = GetProfileSetting(settingName);
            // Make sure we're using a dot (.) as the decimal separator, no matter the system's local settings.
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) ? result : defaultValue;
        }

        #endregion
    }
}
