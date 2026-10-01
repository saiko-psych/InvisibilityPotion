using System.IO;
using System.Reflection;
using Jotunn.Managers;

namespace InvisibilityPotion.Items
{
    public static class Localization
    {
        public static void Register()
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("InvisibilityPotion.Items.English.json"))
            using (var reader = new StreamReader(stream))
                loc.AddJsonFile("English", reader.ReadToEnd());
        }
    }
}
