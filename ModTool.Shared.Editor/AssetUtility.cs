using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ModTool.Shared.Editor
{
    /// <summary>
    /// A set of utilities for handling assets.
    /// </summary>
    public class AssetUtility
    {
        /// <summary>
        /// Finds and returns the directory where ModTool is located.
        /// </summary>
        /// <returns>The directory where ModTool is located.</returns>
        public static string GetModToolDirectory()
        {
            string location = typeof(ModInfo).Assembly.Location;

            string modToolDirectory = Path.GetDirectoryName(location);

            if (!Directory.Exists(modToolDirectory))
                modToolDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");

            return GetRelativePath(modToolDirectory);
        }

        /// <summary>
        /// Get the relative path for an absolute path.
        /// </summary>
        /// <param name="path">The absolute path.</param>
        /// <returns>The relative path.</returns>
        public static string GetRelativePath(string path)
        {
            string currentDirectory = Directory.GetCurrentDirectory();

            Uri pathUri = new Uri(path);

            if (!currentDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                currentDirectory += Path.DirectorySeparatorChar;

            Uri directoryUri = new Uri(currentDirectory);

            string relativePath = Uri.UnescapeDataString(directoryUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));

            return relativePath;
        }

        /// <summary>
        /// Get all asset paths for assets that match the filter.
        /// </summary>
        /// <param name="filter">The filter string can contain search data for: names, asset labels and types (class names).</param>
        /// <returns>A list of asset paths</returns>
        public static List<string> GetAssets(string filter)
        {
            List<string> assetPaths = new List<string>();

            string[] assetGuids = AssetDatabase.FindAssets(filter);

            foreach (string guid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (assetPath.Contains("ModTool"))
                    continue;

                //NOTE: AssetDatabase.FindAssets() can contain duplicates for some reason
                if (assetPaths.Contains(assetPath))
                    continue;

                assetPaths.Add(assetPath);
            }

            return assetPaths;
        }

        /// <summary>
        /// Move assets to a directory.
        /// </summary>
        /// <param name="assetPaths">A list of asset paths</param>
        /// <param name="targetDirectory">The directory to move all assets to.</param>
        /// <param name="includeButNotCopied"></param>
        public static List<string> MoveAssets(List<string> assetPaths, string targetDirectory, out List<string> includeButNotCopied)
        {
            var copiedAssets = new List<string>();
            includeButNotCopied = new List<string>();
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];

                if (Path.GetDirectoryName(assetPath) != targetDirectory)
                {
                    string assetName = Path.GetFileName(assetPath);
                    string newAssetPath = Path.Combine(targetDirectory, assetName);

                    if (assetPath.Contains("Library"))
                    {
                        copiedAssets.Add(Path.Combine(targetDirectory, assetName));
                        assetPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                        newAssetPath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), targetDirectory), assetName);
                        File.Copy(assetPath, newAssetPath, true);
                        AssetDatabase.ImportAsset(Path.Combine(targetDirectory, assetName), ImportAssetOptions.ForceSynchronousImport);
                    }
                    else
                    {
                        AssetDatabase.MoveAsset(assetPath, newAssetPath);
                        includeButNotCopied.Add(newAssetPath);
                    }
                    
                    assetPaths[i] = newAssetPath;
                }
                else
                {
                    includeButNotCopied.Add(assetPath);
                }
            }
            return copiedAssets;
        }

        /// <summary>
        /// Create an asset for a ScriptableObject in a ModTool Resources directory.
        /// </summary>
        /// <param name="scriptableObject">A ScriptableObject instance.</param>
        public static void CreateAsset(ScriptableObject scriptableObject)
        {
            string resourcesParentDirectory = GetModToolDirectory();
            string resourcesDirectory = "";

            resourcesDirectory = Directory.GetDirectories(resourcesParentDirectory, "Resources", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(resourcesDirectory))
            {
                resourcesDirectory = Path.Combine(resourcesParentDirectory, "Resources");
                Directory.CreateDirectory(resourcesDirectory);
            }

            string path = Path.Combine(resourcesDirectory, scriptableObject.GetType().Name + ".asset");

            AssetDatabase.CreateAsset(scriptableObject, path);
        }

        /// <summary>
        /// Change the GUIDs of the given assets.
        /// Empty fields or list will be ignored.
        /// </summary>
        /// <param name="assets"></param>
        /// <param name="guids"></param>
        public static void ChangeGUIDs(List<string> assets, List<string> guids)
        {
            AssetDatabase.StartAssetEditing();
            if (assets == null || guids == null)
                return;

            for (int i = 0; i < assets.Count; i++)
            {
                string guid = "";
                try
                {
                    guid = guids[i];
                }
                catch
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(guid) && IsGuid(guid))
                {
                    try
                    {
                        var metaPath = assets[i] + ".meta";
                        metaPath = Path.Combine(Directory.GetCurrentDirectory(), metaPath);

                        string contents = File.ReadAllText(metaPath);
                        IEnumerable<string> metaGUIDs = GetGuids(contents);
                        contents = contents.Replace("guid: " + metaGUIDs.First(), "guid: " + guids[i]);
                        File.WriteAllText(metaPath, contents);
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e);
                    }
                }
            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        
        private static bool IsGuid(string text) {
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (
                    !((c >= '0' && c <= '9') ||
                      (c >= 'a' && c <= 'z'))
                )
                    return false;
            }

            return true;
        }
        
        private static IEnumerable<string> GetGuids(string text) {
            const string guidStart = "guid: ";
            const int guidLength = 32;
            int textLength = text.Length;
            int guidStartLength = guidStart.Length;
            List<string> guids = new List<string>();

            int index = 0;
            while (index + guidStartLength + guidLength < textLength) {
                index = text.IndexOf(guidStart, index, StringComparison.Ordinal);
                if (index == -1)
                    break;

                index += guidStartLength;
                string guid = text.Substring(index, guidLength);
                index += guidLength;

                if (IsGuid(guid)) {
                    guids.Add(guid);
                }
            }

            return guids;
        }
    }
}
