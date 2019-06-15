namespace Someta
{
    public interface IStateInterceptor
    {
    }

    public interface IStateInterceptor<T> : IStateInterceptor where T : InterceptorScopes.Scope
    {
    }
}