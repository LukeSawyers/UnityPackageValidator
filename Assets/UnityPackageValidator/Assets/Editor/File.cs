
namespace UnityPackageValidator
{
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