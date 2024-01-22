namespace JLUtility
{
    public abstract class FileObject
    {
        internal List<Action> FileChangeCallback { get; private set; } = new List<Action>();
        public string Path { get; internal set; }
        internal void InvokeOnFileUpdate()
        {
            for (int i = 0; i < FileChangeCallback.Count; i++)
            {
                FileChangeCallback[i].Invoke();
            }
        }
        internal FileObject(string path)
        {
            Path = path;
        }
        ~FileObject()
        {
            FileChangeCallback = null;
        }
    }
}
