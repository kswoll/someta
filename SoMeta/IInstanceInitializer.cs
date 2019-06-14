namespace Someta
{
    public interface IInstanceInitializer : IClassInterceptor
    {
        void Initialize(object instance);
    }
}