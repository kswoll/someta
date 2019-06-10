namespace SoMeta.Helpers
{
    public class AsyncInvoker
    {
        public override async Task<object> Proceed()
        {
            return await implementation(this);
        }
        
    }
}