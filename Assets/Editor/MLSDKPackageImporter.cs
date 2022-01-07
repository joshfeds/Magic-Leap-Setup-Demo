#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public class MLSDKPackageImporter
{
    private const float PACKAGE_INSTALL_TIMEOUT = 5f;

    private static readonly string ManifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
    
    private static readonly string ML_UNITY_SDK = "com.magicleap.unitysdk"; 

    private static readonly Manifest PackageManifest = new Manifest(ManifestPath);

    private static readonly Manifest.ScopedRegistry ML_REGISTRY = new Manifest.ScopedRegistry()
    {
        name = "Magic Leap",
        url = "https://registry.npmjs.org",
        scopes = new[] { "com.magicleap" }
    };

    static MLSDKPackageImporter()
    {
        if (Application.isBatchMode)
            return;

        if (!ContainsMLRegistry(PackageManifest))
            ShowPopup();
    }

    [MenuItem("Magic Leap/First Time Setup")]
    private static void ShowPopup()
    {
        Debug.Log("MLSDK Setup: performing first time setup...");
        if (EditorUtility.DisplayDialog("Magic Leap Unity SDK First Time Setup", "Would you like to set up this project for use with the Magic Leap Unity SDK?\n\n(This can be done at any time with the \"Magic Leap -> First Time Setup\" menu item.)", "Ok", "Not right now"))
            DoFirstTimeSetup();
    }

    //[MenuItem("Magic Leap/First Time Setup", true)]
    //private static bool CanShowMenuItem() => !ContainsMLRegistry(PackageManifest);

    private static void DoFirstTimeSetup()
    {
        if (!ContainsMLRegistry(PackageManifest))
        {
            Debug.Log("MLSDK Setup: ML registry is not present. Adding...");
            AddMLRegistryToManifest(PackageManifest);
        }
        else
            Debug.Log("MLSDK Setup: ML registry already exists.");

        if (!VerifyUnitySDK())
        {
            Debug.Log("MLSDK Setup: setup finished with errors. See previous logs for more details.");
            return;
        }
        Debug.Log("MLSDK Setup: setup complete!");

        if(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Lumin)
            EditorUtility.DisplayDialog("Magic Leap Unity SDK First Time Setup", "First time setup complete!", "Ok");
        else
            EditorUtility.DisplayDialog("Magic Leap Unity SDK First Time Setup", "First time setup complete!\n\nPlease switch to the Lumin build target to get started. (File -> Build Settings)", "Ok");
    }

    private static bool VerifyUnitySDK() 
    {
        AddRequest addRequest = Client.Add(ML_UNITY_SDK);

        // wait until request is complete or the timeout passed 
        float currTime, startTime;
        currTime = startTime = Time.realtimeSinceStartup;
        while (!addRequest.IsCompleted || startTime + PACKAGE_INSTALL_TIMEOUT > currTime)
            currTime = Time.realtimeSinceStartup;

        if (!addRequest.IsCompleted)
        {
            Debug.LogError("MLSDK Setup: package installation timeout");
            return false;
        }
        else
        {
            if (addRequest.Error != null)
            {
                switch (addRequest.Error.errorCode)
                {
                    case ErrorCode.Conflict:
                        Debug.Log("MLSDK Setup: SDK package already exists");
                        return true;
                    case ErrorCode.Forbidden:
                        Debug.LogError("MLSDK Setup: package permission denied");
                        break;
                    case ErrorCode.InvalidParameter:
                        Debug.LogError("MLSDK Setup: invalid parameters");
                        break;
                    case ErrorCode.NotFound:
                        Debug.LogError("MLSDK Setup: package was not found");
                        break; 
                    case ErrorCode.Unknown:
                        Debug.LogError("MLSDK Setup: unknown error in package installation");
                        break; 
                }
                return false;
            }
            else
                Debug.Log("MLSDK Setup: package added successfully");
        }
        
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if the given Manifest contains the ML registry
    /// </summary>
    static bool ContainsMLRegistry(Manifest manifest) =>
        manifest.scopedRegistries.ToList().Any(d => d.name == ML_REGISTRY.name);

    /// <summary>
    /// Updates the given Manifest to include the ML registry
    /// </summary>
    static void AddMLRegistryToManifest(Manifest manifest)
    {
        // first remove old registry if it exists
        var registries = manifest.scopedRegistries.ToList();

        registries.Add(ML_REGISTRY);
        manifest.scopedRegistries = registries.ToArray();
        manifest.Serialize();

        AssetDatabase.Refresh();
    }
  

    private class Manifest
    {
        /// <summary>
        /// File format for manifests
        /// </summary>
        private class ManifestFile
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
            public ScopedRegistry[] scopedRegistries;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
            public Dependencies dependencies;
        }

        /// <summary>
        /// File format for manifests without any registries
        /// </summary>
        private class ManifestFileWithoutRegistries
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
            public Dependencies dependencies;
        }

        /// <summary>
        /// Dummy struct for encapsulation -- dependencies are manually handled via direct string manipulation
        /// </summary>
        [Serializable]
        public struct Dependencies
        { }

        [Serializable]
        public struct ScopedRegistry
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
            public string name;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
            public string url;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
            public string[] scopes;
        }


        private const int INDEX_NOT_FOUND_ERROR = -1;
        private const string DEPENDENCIES_KEY = "\"dependencies\"";

        public string Path { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
        public string dependencies;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "manifest.json syntax")]
        public ScopedRegistry[] scopedRegistries;

        public Manifest(string path)
        {
            Path = path;
            string fullJsonString = File.ReadAllText(path);
            var manifestFile = JsonUtility.FromJson<ManifestFile>(fullJsonString); 

            scopedRegistries = manifestFile.scopedRegistries ?? new ScopedRegistry[0];
            var startIndex = GetDependenciesStart(fullJsonString);
            var endIndex = GetDependenciesEnd(fullJsonString, startIndex);

            dependencies = (startIndex == INDEX_NOT_FOUND_ERROR || endIndex == INDEX_NOT_FOUND_ERROR) ? null : fullJsonString.Substring(startIndex, endIndex - startIndex);
        }

        public void Serialize()
        {
            string jsonString = (scopedRegistries.Length > 0) ?
                JsonUtility.ToJson(new ManifestFile { scopedRegistries = scopedRegistries, dependencies = new Dependencies() }, true) :
                JsonUtility.ToJson(new ManifestFileWithoutRegistries() { dependencies = new Dependencies() }, true);

            int startIndex = GetDependenciesStart(jsonString);
            int endIndex = GetDependenciesEnd(jsonString, startIndex);

            var stringBuilder = new StringBuilder();
            stringBuilder.Append(jsonString.Substring(0, startIndex));
            stringBuilder.Append(dependencies);
            stringBuilder.Append(jsonString.Substring(endIndex, jsonString.Length - endIndex));

            File.WriteAllText(Path, stringBuilder.ToString());
        }

        static int GetDependenciesStart(string json)
        {
            int dependenciesIndex = json.IndexOf(DEPENDENCIES_KEY, StringComparison.InvariantCulture);
            if (dependenciesIndex == INDEX_NOT_FOUND_ERROR)
                return INDEX_NOT_FOUND_ERROR;

            int dependenciesStartIndex = json.IndexOf('{', dependenciesIndex + DEPENDENCIES_KEY.Length);
            if (dependenciesStartIndex == INDEX_NOT_FOUND_ERROR)
                return INDEX_NOT_FOUND_ERROR;

            dependenciesStartIndex++;
            return dependenciesStartIndex;
        }

        static int GetDependenciesEnd(string jsonString, int dependenciesStartIndex) => jsonString.IndexOf('}', dependenciesStartIndex);
    }
}
#endif