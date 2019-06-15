namespace Someta
{
    public class ExtensionPointScopes
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