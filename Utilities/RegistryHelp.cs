using System;
using Microsoft.Win32;

namespace TakeoverDefender.Utilities
{
    internal static class RegistryHelp
    {
        private static RegistryView DefaultView => Environment.Is64BitOperatingSystem
            ? RegistryView.Registry64
            : RegistryView.Registry32;

        private static RegistryKey GetWriteKey(RegistryHive hive)
        {
            return RegistryKey.OpenBaseKey(hive, DefaultView);
        }

        internal static void Write(RegistryKey root, string subkey, string name, object data, RegistryValueKind kind)
        {
            try
            {
                using RegistryKey baseKey = GetWriteKey(GetHive(root));
                using RegistryKey key = baseKey.CreateSubKey(subkey, true);
                key?.SetValue(name, data, kind);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Registry write failed: {subkey}\\{name} - {ex.Message}");
            }
        }

        internal static void DeleteValue(RegistryKey root, string subkey, string name)
        {
            try
            {
                using RegistryKey baseKey = GetWriteKey(GetHive(root));
                using RegistryKey key = baseKey.OpenSubKey(subkey, true);
                if (key?.GetValue(name) != null)
                    key.DeleteValue(name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Registry delete failed: {subkey}\\{name} - {ex.Message}");
            }
        }

        internal static void DeleteFolder(RegistryKey root, string subkey)
        {
            try
            {
                using RegistryKey baseKey = GetWriteKey(GetHive(root));
                baseKey.DeleteSubKeyTree(subkey, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Registry delete folder failed: {subkey} - {ex.Message}");
            }
        }

        internal static T GetValue<T>(string subKey, string valueName, T defaultValue)
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, DefaultView);
                object value = baseKey.OpenSubKey(subKey.Replace(@"HKEY_LOCAL_MACHINE\", ""))?.GetValue(valueName, null);
                if (value == null)
                    return defaultValue;
                if (value is T t)
                    return t;
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        private static RegistryHive GetHive(RegistryKey key)
        {
            if (key == Registry.LocalMachine) return RegistryHive.LocalMachine;
            if (key == Registry.CurrentUser) return RegistryHive.CurrentUser;
            if (key == Registry.ClassesRoot) return RegistryHive.ClassesRoot;
            if (key == Registry.Users) return RegistryHive.Users;
            return RegistryHive.LocalMachine;
        }
    }
}
