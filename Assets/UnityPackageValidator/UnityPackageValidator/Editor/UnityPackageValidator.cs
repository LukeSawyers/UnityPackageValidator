using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityPackageValidator
{
    /// <summary>
    /// Package manager holds a list of root folders that correspond to 
    /// exportable UnityPackages. 
    /// Checks the dependencies of these packages to ensure their dependencies are correct
    /// </summary>
    public class UnityPackageValidator : EditorWindow
    {
        // holding an instance allows method to be called from command line
        private static UnityPackageValidator Instance;

        // pacakage manifest
        private enum ManifestReadState
        {
            None,
            Packages,
            Dependencies
        }

        private string _packageManifest = "";

        // Package manifest editor
        private Vector2 _scrollViewPos = Vector2.zero;

        private string PackageManifestText
        {
            set
            {
                if(value != _packageManifestText)
                {
                    _packageManifestWindowText = value;
                    _packageManifestText = value;
                }
            }
        }

        private string _packageManifestText = "";

        private string _packageManifestWindowText = "";

        private int _packageManifestLines = 1;

        // packages
        private List<UnityPackage> _packages = new List<UnityPackage>();

        private List<UnityPackageTopView> _packageViews = new List<UnityPackageTopView>();

        // class references
        private bool IncludeClassDependencies = false;

        public static void ExportUnityPackages()
        {
            Instance = new UnityPackageValidator();
            Instance.ExportPackages();
        }

        [MenuItem("Tools/Unity Package Validator")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            UnityPackageValidator window = (UnityPackageValidator)EditorWindow.GetWindow(typeof(UnityPackageValidator), false, "Unity Package Validator");
            window.Show();
            Instance = window;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(EditorStyles.largeLabel);
            style.fontSize = 18;
            GUILayout.Label("Unity Package Validator", style);
            GUILayout.Label("A utility to help resolve dependencies between unity packages");
            GUILayout.Label("NOTE: This does not apply to script references");

            GetPackageManifest();

            RenderManifestWindow();

            // Get the package manifest
            if(GUILayout.Button("Validate Packages"))
            {
                GetPackages(IncludeClassDependencies);
            }
            IncludeClassDependencies = GUILayout.Toggle(IncludeClassDependencies, "Include class dependencies");

            ShowPackages();

            // Export the packages
            if (GUILayout.Button("Export All Packages"))
            {
                ExportPackages();
            }
        }

        /// <summary>
        /// Acquires the path reference for the package manifest file and reads the text
        /// </summary>
        private void GetPackageManifest()
        {
            // get the manifest path
            var files = AssetDatabase.FindAssets("UnityPackageManifest");

            if (files == null || files.Length == 0)
            {
                Debug.LogWarning("UnityPackageManager: No Package Manifest file found");
                return;
            }
            else if (files.Length > 1)
            {
                Debug.LogWarning("UnityPackageManager: More than one package manifest file found, taking arbitrary first: " + AssetDatabase.GUIDToAssetPath(files[0]));
            }
            _packageManifest = AssetDatabase.GUIDToAssetPath(files[0]);
            _packageManifestLines = _packageManifestWindowText.Split(new string[] { "\n" }, System.StringSplitOptions.None).Length;
            PackageManifestText = System.IO.File.ReadAllText(_packageManifest);
        }

        /// <summary>
        /// Renders the manifest editor window
        /// </summary>
        private void RenderManifestWindow()
        {
            EditorGUILayout.LabelField("Package Manifest:", EditorStyles.boldLabel);
            _packageManifestWindowText = EditorGUILayout.TextArea(_packageManifestWindowText, GUILayout.Height((_packageManifestLines + 2) * 12));

            if (GUILayout.Button("Save Manifest File"))
            {
                System.IO.File.WriteAllText(_packageManifest, _packageManifestWindowText);
                GetPackages(IncludeClassDependencies);
            }
        }

        /// <summary>
        /// Renders all packages found
        /// </summary>
        private void ShowPackages()
        {
            _scrollViewPos = EditorGUILayout.BeginScrollView(_scrollViewPos);
            foreach (var package in _packageViews)
            {
                package.Render();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Gets packages from the repository based on the manifest and validates them
        /// </summary>
        private void GetPackages(bool includeClassDependencies)
        {
            var packages = ReadPackageManifest();
            if (packages == null)
            {
                Debug.LogWarning("Could not read manifest file");
                return;
            }

            // add new package objects to list 
            var existingPackageNames = _packages.Select(p => p.Name);
            var newPackageNames = packages.Select(p => p.Name);

            foreach (var newPackage in packages)
            {
                if (!existingPackageNames.Contains(newPackage.Name))
                {
                    _packages.Add(newPackage);
                    _packageViews.Add(new UnityPackageTopView(newPackage));
                }
                else
                {
                    // if it already exists update the dependency list
                    var package = _packages.First(p => p.Name == newPackage.Name);
                    package.UpdatePackageDependencies(newPackage.Dependencies, IncludeClassDependencies);
                }
            }

            // remove packages if they no longer exist
            foreach (var existingPackage in packages)
            {
                if (!newPackageNames.Contains(existingPackage.Name))
                {
                    _packages.Remove(existingPackage);
                    _packageViews.Remove(_packageViews.FirstOrDefault(p => p.Package == existingPackage));
                }
            }
        }
        
        /// <summary>
        /// Reads the package manifest
        /// </summary>
        /// <returns></returns>
        private List<UnityPackage> ReadPackageManifest()
        {

            // check the file exists before proceeding
            if (!System.IO.File.Exists(_packageManifest))
            {
                return null;
            }

            // read the file

            List<UnityPackage> packages = new List<UnityPackage>();

            var lines = System.IO.File.ReadAllLines(_packageManifest);

            ManifestReadState readState = ManifestReadState.None;

            foreach (var line in lines)
            {
                if (System.String.IsNullOrEmpty(line))
                {
                    continue;
                }

                // determine state
                if (line.ToLower().Contains("packages:"))
                {
                    readState = ManifestReadState.Packages;
                    continue;
                }
                else if (line.ToLower().Contains("dependencies:"))
                {
                    readState = ManifestReadState.Dependencies;
                    continue;
                }

                // read the line
                switch (readState)
                {
                    case ManifestReadState.Packages:
                        string dir = Application.dataPath + "\\" + line;
                        if (System.IO.Directory.Exists(dir))
                        {
                            var newPackage = new UnityPackage(line);
                            packages.Add(newPackage);
                        }
                        break;
                    case ManifestReadState.Dependencies:
                        var args = line.Split(' ');
                        if (args.Length < 2)
                        {
                            continue;
                        }

                        var package = packages.FirstOrDefault(p => p.Name == args[0]);
                        if (package == null)
                        {
                            continue;
                        }

                        for (int i = 1; i < args.Length; i++)
                        {
                            var dependency = packages.FirstOrDefault(p => p.Name == args[i]);
                            if (dependency == null)
                            {
                                continue;
                            }
                            package.Dependencies.Add(dependency);
                        }
                        break;
                    case ManifestReadState.None:

                        break;
                    default:
                        throw new System.ArgumentOutOfRangeException();
                }
            }

            foreach(var package in packages)
            {
                package.UpdateFileLists(IncludeClassDependencies);
            }

            return packages;
        }

        /// <summary>
        /// Exports all currently held packages
        /// </summary>
        public void ExportPackages()
        {
            GetPackageManifest();
            RenderManifestWindow();
            GetPackages(IncludeClassDependencies);
            foreach (UnityPackage p in _packages)
            {
                p.ExportPackage(false);
            }
        }
    }

}
