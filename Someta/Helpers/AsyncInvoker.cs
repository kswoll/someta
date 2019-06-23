using System.Threading.Tasks;

namespace Someta.Helpers
{
    /// <summary>
    /// Helper class called by the async method interceptor weaver to wrap and unwrap tasks
    /// to and from `Task{object}` and `Task{T}`
    /// </summary>
    public static class AsyncInvoker
    {
        public static async Task<object> Wrap<T>(Task<T> task)
        {
            return await task;
        }

        public static async Task<T> Unwrap<T>(Task<object> task)
        {
            return (T)await task;
        }
    }
}