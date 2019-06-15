using System.Reflection;

namespace Someta
{
    public interface IInstanceInitializer : IClassInterceptor
    {
        void Initialize(object instance, MemberInfo member);
    }

    public interface IInstanceInitializer<T> : IInstanceInitializer where T : InterceptorScopes.Scope
    {
    }
}