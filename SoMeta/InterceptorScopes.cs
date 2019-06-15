namespace Someta
{
    public class InterceptorScopes
    {
        public interface Scope
        {
        }

        public interface Property : Scope
        {
        }

        public interface Method : Scope
        {
        }

        public interface Class : Scope
        {
        }
    }
}