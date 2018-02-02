using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityPackageValidator
{
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
        public void UpdateFileLists(bool includeClassDependencies)
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
            foreach (var path in paths)
            {
                var ind = path.IndexOf("Assets");
                var cutPath = path.Remove(0, ind);
                cutPaths.Add(cutPath);
            }

            // get the dependencies for all of the files found
            var dependencies = AssetDatabase.GetDependencies(cutPaths.ToArray(), true);
            foreach (var file in dependencies)
            {
                bool isExternal = true;
                if (file.ToLower().Contains(Name.ToLower()))
                {
                    continue;
                }
                foreach (var package in Dependencies)
                {
                    if (file.ToLower().Contains(package.Name.ToLower() + '/'))
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

            if (includeClassDependencies)
            {
                GetScriptReferences();
            }

        }

        // TODO refactor with regex
        private void GetScriptReferences()
        {
            // compile a list of all .cs files in this project
            List<string> filePaths = new List<string>();
            var info = new DirectoryInfo(Application.dataPath);
            var fileInfo = info.GetFiles("*.cs", SearchOption.AllDirectories);
            foreach (var file in fileInfo)
            {
                if (System.IO.File.Exists(file.ToString()))
                {
                    filePaths.Add(file.ToString());
                }
            }

            // get all files that are in this package
            var thisFilePaths = filePaths.Where(f => f.Contains(Name + "\\")).ToList();

            // get all files that are not in this package or any dependency package
            var otherFilePaths = filePaths.Where(f => !PathContainedInPackageOrDependency(f)).ToList();

            // get class, interface and struct names
            List<string> typeNames = new List<string>();
            foreach(var file in otherFilePaths)
            {
                // search for any line that has the words class inteface or struct in it and is not a comment
                var fileText = System.IO.File.ReadAllText(file);


            }
        }

        private bool PathContainedInPackageOrDependency(string path)
        {
            if(path.Contains(Name + "\\"))
            {
                return true;
            }
            foreach(var dependency in _dependencies)
            {
                if (path.Contains(dependency.Name + "\\"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Updates the package dependencies
        /// </summary>
        /// <param name="dependencies"></param>
        public void UpdatePackageDependencies(List<UnityPackage> dependencies, bool includeClassDependencies)
        {
            _dependencies = dependencies;
            UpdateFileLists(includeClassDependencies);
        }

        /// <summary>
        /// Exports this package
        /// </summary>
        public void ExportPackage(bool IncludeDependentPackages)
        {
            List<string> paths = new List<string>();
            paths.Add("Assets\\" + Name);
            if (IncludeDependentPackages)
            {
                foreach(var package in _dependencies)
                {
                    paths.Add("Assets\\" + package.Name);
                }
            }
            AssetDatabase.ExportPackage(paths.ToArray(), Name + ".unitypackage", ExportPackageOptions.Interactive | ExportPackageOptions.Recurse);
        }

        private string _name;
        private List<UnityPackage> _dependencies = new List<UnityPackage>();
        private List<File> _externalDependencies = null;
        private List<File> _files = null;
        private List<File> _filesWithBadDependencies = null;
    }
}