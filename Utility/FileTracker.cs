using System;

namespace JLUtility
{
    public class FileTracker
    {
        struct FileWatcher {
            public List<IFileObject> fileObjects;
            public List<DateTime> LastTouched;
            public FileSystemWatcher systemWatcher;
        }
        private Dictionary<string, FileWatcher> directoryFilePair = new Dictionary<string, FileWatcher>();

        bool TryGetValue(string dir, out FileWatcher fileWatcher)
        {
            return directoryFilePair.TryGetValue(dir, out fileWatcher);
        }
        public void AddFileObject(IFileObject fileObject)
        {
            var dirPath = Path.GetFullPath(fileObject.FilePath);
            dirPath = Path.GetDirectoryName(dirPath);
            var file = Path.GetFileName(fileObject.FilePath);
            Debug.Log("FileWatcher: Added directory: " + dirPath);
            Debug.Log("FileWatcher: Added file: " + file);

            if (TryGetValue(dirPath, out var watcher))
            {
                watcher.fileObjects.Add(fileObject);
                watcher.LastTouched.Add(DateTime.MinValue);
            }
            else
            {

                directoryFilePair.Add(dirPath,
                    new FileWatcher()
                    {
                        fileObjects = new List<IFileObject>(),
                        LastTouched = new List<DateTime>(),
                        systemWatcher = CreateFileWatcher(dirPath)
                    });
                directoryFilePair[dirPath].fileObjects.Add(fileObject);
                directoryFilePair[dirPath].LastTouched.Add(DateTime.MinValue);
            }
        }
        public void RemoveFileObject(IFileObject fileObject)
        {
            if (TryGetValue(Path.GetDirectoryName(fileObject.FilePath), out var watcher))
            {
                watcher.fileObjects.Remove(fileObject);
            }
        }
        FileSystemWatcher CreateFileWatcher(string path)
        {
            // Create a new FileSystemWatcher and set its properties.
            FileSystemWatcher watcher = new FileSystemWatcher(path);
            /* Watch for changes in LastAccess and LastWrite times, and 
               the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            // Only watch text files.
            watcher.Filter = "*.*";

            // Add event handlers.
            watcher.Changed += (a, b) => {
                Queue.Add(new FileSystemEvent(){
                    source = a,
                    e = b,
                    handler = new FileSystemEventHandler(OnChanged)
                });
            };
            watcher.Created += (a, b) => {
                Queue.Add(new FileSystemEvent()
                {
                    source = a,
                    e = b,
                    handler = new FileSystemEventHandler(OnChanged)
                });
            };
            watcher.Deleted += (a, b) => {
                Queue.Add(new FileSystemEvent()
                {
                    source = a,
                    e = b,
                    handler = new FileSystemEventHandler(OnChanged)
                });
            };
            watcher.Renamed += (a, b) => {
                Queue.Add(new FileSystemEvent()
                {
                    source = a,
                    e = b,
                    handler = new RenamedEventHandler(OnRenamed)
                });
            };

            // Begin watching.
            watcher.EnableRaisingEvents = true;

            return watcher;
        }
        struct FileSystemEvent
        {
            public object source;
            public EventArgs e;
            public Delegate handler;
        }
        public void ResolveFileTrackQueue()
        {
            try
            {
                for (int i = 0; i < Queue.Count; i++)
                {

                    Queue[i].handler.DynamicInvoke(Queue[i].source, Queue[i].e);

                }
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
            finally
            {
                Queue.Clear();
            }
        }
        List<FileSystemEvent> Queue = new List<FileSystemEvent>();
        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
            var path = Path.GetDirectoryName(e.FullPath);
            var fileName = Path.GetFileName(e.FullPath);
            var objects = directoryFilePair[path].fileObjects;
            var touched = directoryFilePair[path].LastTouched;
            for (int i = 0; i < objects.Count; i++)
            {
                var f = Path.GetFileName(objects[i].FilePath);
                if (f == fileName)
                {
                    if (lastWriteTime == touched[i])
                    {
                        break;
                    }
                    Debug.Log(lastWriteTime + ", " + touched[i]);
                    Debug.Log("File: " + e.FullPath + " " + e.ChangeType);
                    touched[i] = lastWriteTime;
                    objects[i].InvokeOnFileUpdate();
                    break;
                }
            }
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            // Specify what is done when a file is renamed.
            Debug.LogFormat("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
        }
    }
}
