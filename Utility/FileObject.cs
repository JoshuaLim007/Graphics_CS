using JLGraphics;

namespace JLUtility
{
    public interface IFileObject
    {
        public List<Action> FileChangeCallback { get; }
        public string FilePath { get; }
        internal void InvokeOnFileUpdate()
        {
            for (int i = 0; i < FileChangeCallback.Count; i++)
            {
                FileChangeCallback[i].Invoke();
            }
        }
    }
}
