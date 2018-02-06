using UnityEngine;
using UnityEditor;

namespace UnityPackageValidator
{
    /// <summary>
    /// The top package view
    /// </summary>
    public class UnityPackageTopView
    {
        public bool TopFoldout { get; set; }
        public bool ExportFoldout { get; set; }
        public bool PackageDependenciesFoldout { get; set; }
        public bool ExternalDependenciesFoldout { get; set; }

        public bool ExportWithDependendantPackages { get; set; }

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

            // export options
            ExportFoldout = EditorGUILayout.Foldout(ExportFoldout, "Export Options");
            if (ExportFoldout)
            {
                if (GUILayout.Button("Export Package", EditorStyles.miniButtonLeft))
                {
                    Package.ExportPackage(ExportWithDependendantPackages);
                }
                ExportWithDependendantPackages = GUILayout.Toggle(ExportWithDependendantPackages, "Export With Dependent Packages");
            }

            EditorGUI.indentLevel--;
        }
    }
}