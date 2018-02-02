using System.IO;
using System.Linq;
using System.Collections;
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
        private enum ManifestReadState
        {
            None,
            Packages,
            Dependencies
        }

        private static UnityPackageValidator Instance;

        private Vector2 _scrollViewPos = Vector2.zero;

        private string _packageManifest = "";

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

        private List<UnityPackage> _packages = new List<UnityPackage>();

        private List<UnityPackageTopView> _packageViews = new List<UnityPackageTopView>();

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
                GetPackages();
            }

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
                GetPackages();
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
        private void GetPackages()
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
                    package.UpdatePackageDependencies(newPackage.Dependencies);
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
                package.UpdateFileLists();
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
            GetPackages();
            foreach (UnityPackage p in _packages)
            {
                p.ExportPackage();
            }
        }
    }

    /// <summary>
    /// The top package view
    /// </summary>
    public class UnityPackageTopView
    {
        public bool TopFoldout { get; set; }
        public bool PackageDependenciesFoldout { get; set; }
        public bool ExternalDependenciesFoldout { get; set; }

        public UnityPackage Package
        {
            get
            {
                return _package;
            }
        }

        private UnityPackage _package;

        public UnityPackageTopView(UnityPackage package)
        {
            _package = package;
            TopFoldout = false;
            PackageDependenciesFoldout = false;
            ExternalDependenciesFoldout = false;
        }

        /// <summary>
        /// Renders this item
        /// </summary>
        public void Render()
        {
            GUIStyle topFoldout = new GUIStyle(EditorStyles.foldout);
            topFoldout.fontStyle = FontStyle.Bold;
            TopFoldout = EditorGUILayout.Foldout(TopFoldout, _package.Name, topFoldout);
            if (!TopFoldout)
            {
                return;
            }

            if(GUILayout.Button("Export Package", EditorStyles.miniButtonLeft))
            {
                Package.ExportPackage();
            }

            EditorGUI.indentLevel++;
            // show dependencies
            if (_package.Dependencies.Count != 0)
            {
                PackageDependenciesFoldout = EditorGUILayout.Foldout(PackageDependenciesFoldout, "Pacakge Dependencies: " + _package.Dependencies.Count);
                if (PackageDependenciesFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var dep in _package.Dependencies)
                    {
                        EditorGUILayout.LabelField(" - " + dep.Name);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Pacakge Dependencies: 0");
            }
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel++;
            // show external references
            if (_package.ExternalDependencies.Count != 0)
            {
                GUIStyle myFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                Color myStyleColor = Color.red;
                myFoldoutStyle.normal.textColor = myStyleColor;

                ExternalDependenciesFoldout = EditorGUILayout.Foldout(ExternalDependenciesFoldout, "External Dependencies: " + _package.ExternalDependencies.Count, myFoldoutStyle);
                if (ExternalDependenciesFoldout)
                {
                    
                    EditorGUI.indentLevel++;
                    foreach (var dep in _package.ExternalDependencies)
                    {
                        EditorGUILayout.LabelField(" - " + dep.Path);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                GUIStyle myLabelStyle = new GUIStyle(EditorStyles.label);
                Color myStyleColor = Color.green;
                myLabelStyle.normal.textColor = myStyleColor;
                EditorGUILayout.LabelField("External Dependencies: 0", myLabelStyle);
            }
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// Represents a unity package 
    /// </summary>
    public class UnityPackage
    {
        /// <summary>
        /// The name of this package
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        /// <summary>
        /// The packages this package depends on
        /// </summary>
        public List<UnityPackage> Dependencies
        {
            get
            {
                return _dependencies;
            }
        }

        /// <summary>
        /// Returns all files that will be included in this file
        /// </summary>
        public List<File> Files
        {
            get
            {
                return _files;
            }
        }

        /// <summary>
        /// List of all dependencies external to this package or its package dependencies
        /// </summary>
        public List<File> ExternalDependencies
        {
            get
            {
                return _externalDependencies;
            }
        }

        public UnityPackage(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Refreshes the file list for this package
        /// </summary>
        public void UpdateFileLists()
        {
            // update file list
            _files = new List<File>();
            var info = new DirectoryInfo(Application.dataPath + "\\" + Name);
            var fileInfo = info.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (var file in fileInfo)
            {
                if (System.IO.File.Exists(file.ToString()))
                {
                    File f = new File(file.ToString(), this);
                }
            }

            // update external dependencies list
            _externalDependencies = new List<File>();
            var paths = _files.Select(f => f.Path).ToArray();
            List<string> cutPaths = new List<string>();
            foreach(var path in paths)
            {
                var ind = path.IndexOf("Assets");
                var cutPath = path.Remove(0, ind);
                cutPaths.Add(cutPath);
            }
            
            var dependencies = AssetDatabase.GetDependencies(cutPaths.ToArray(), true);
            foreach (var file in dependencies)
            {
                bool isExternal = true;
                if (file.ToLower().Contains(Name.ToLower()))
                {
                    continue;
                }
                foreach(var package in Dependencies)
                {
                    if (file.ToLower().Contains(package.Name.ToLower()))
                    {
                        isExternal = false;
                        continue;
                    }
                }
                if (isExternal)
                {
                    File f = new File(file, this);
                    _externalDependencies.Add(f);
                }
            }
        }

        /// <summary>
        /// Updates the package dependencies
        /// </summary>
        /// <param name="dependencies"></param>
        public void UpdatePackageDependencies(List<UnityPackage> dependencies)
        {
            _dependencies = dependencies;
            UpdateFileLists();
        }

        /// <summary>
        /// Exports this package
        /// </summary>
        public void ExportPackage()
        {
            AssetDatabase.ExportPackage("Assets\\" + Name, Name + ".unitypackage", ExportPackageOptions.Interactive | ExportPackageOptions.Recurse);
        }

        private string _name;
        private List<UnityPackage> _dependencies = new List<UnityPackage>();
        private List<File> _externalDependencies = null;
        private List<File> _files = null;
        private List<File> _filesWithBadDependencies = null;
    }

    /// <summary>
    /// Represents a file
    /// </summary>
    public class File
    {

        public string Path
        {
            get
            {
                return _path;
            }
        }

        public File(string path, UnityPackage package)
        {
            _path = path;
            _package = package;
            package.Files.Add(this);
        }

        private string _path;
        private UnityPackage _package;
    }
}
