using System.Threading.Tasks;

namespace Someta.Helpers
{
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