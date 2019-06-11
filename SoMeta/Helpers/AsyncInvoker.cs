using System.Threading.Tasks;

namespace SoMeta.Helpers
{
    public static class AsyncInvoker
    {
        public static async Task<T> InvokeAsync<T>(Task<object> task)
        {
            return (T)await task;
        }
    }
}