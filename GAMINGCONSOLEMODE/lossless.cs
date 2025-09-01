using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace GAMINGCONSOLEMODE // Ihr Namespace
{
    /// <summary>
    /// A robust and future-proof class for reading and writing individual settings
    /// for the "Standard" profile in Lossless Scaling's Settings.xml.
    /// It uses LINQ to XML to avoid breaking when new settings are added to the file.
    /// </summary>
    public static class LosslessSettingsManager
    {
        private static string GetConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lossless Scaling", "Settings.xml");
        }

        /// <summary>
        /// Gets the "Standard" profile XML node from the document.
        /// </summary>
        private static (XElement, string) GetStandardProfileNode(XDocument doc)
        {
            XElement standardProfile = doc.Root
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
        /// Reads a specific setting value from the "Standard" profile by its name.
        /// </summary>
        /// <param name="settingName">The exact name of the XML tag (e.g., "DrawFps").</param>
        /// <returns>The setting's value as a string, or null if not found or an error occurs.</returns>
        public static string GetProfileSetting(string settingName)
        {
            try
            {
                string configPath = GetConfigPath();
                if (!File.Exists(configPath)) return null;

                XDocument doc = XDocument.Load(configPath);
                var (standardProfile, error) = GetStandardProfileNode(doc);
                if (error != null) return null;

                XElement settingElement = standardProfile.Element(settingName);
                return settingElement?.Value;
            }
            catch { return null; }
        }

        /// <summary>
        /// Writes a specific setting value to the "Standard" profile.
        /// If the setting does not exist, it will be created.
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

                XElement settingElement = standardProfile.Element(settingName);
                string valueStr = Convert.ToString(value, CultureInfo.InvariantCulture);

                if (settingElement != null)
                {
                    // If the element exists, update its value
                    settingElement.Value = valueStr;
                }
                else
                {
                    // If the element does not exist, create and add it
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

        #region Convenience Helper Methods for Type Conversion

        public static bool GetProfileSettingAsBool(string settingName, bool defaultValue = false)
        {
            string value = GetProfileSetting(settingName);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        public static int GetProfileSettingAsInt(string settingName, int defaultValue = 0)
        {
            string value = GetProfileSetting(settingName);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public static double GetProfileSettingAsDouble(string settingName, double defaultValue = 0.0)
        {
            string value = GetProfileSetting(settingName);
            // Use InvariantCulture to handle dots (.) as decimal separators correctly
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) ? result : defaultValue;
        }

        #endregion
    }
}