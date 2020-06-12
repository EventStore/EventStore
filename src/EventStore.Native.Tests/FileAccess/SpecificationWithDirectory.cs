using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using EventStore.Native;

namespace EventStore.Native.FileAccess.Tests
{
    public class SpecificationWithDirectory
    {
        protected string PathName;

        protected string GetTempFilePath()
        {
            return Path.Combine(PathName, $"{Guid.NewGuid()}-{GetType().FullName}");
        }

        protected string GetFilePathFor(string fileName)
        {
            return Path.Combine(PathName, fileName);
        }

        [SetUp]
        public virtual Task SetUp()
        {
            var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
            PathName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-{typeName}");
            Directory.CreateDirectory(PathName);

            return Task.CompletedTask;
        }

        [TearDown]
        public virtual Task TearDown()
        {
            //kill whole tree
            ForceDeleteDirectory(PathName);

            return Task.CompletedTask;
        }

        private static void ForceDeleteDirectory(string path)
        {
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };
            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(true);
        }
    }
}
