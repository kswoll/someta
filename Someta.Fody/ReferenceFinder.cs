using Mono.Cecil;
using System;
using System.Linq;

namespace Someta.Fody
{
    internal static class ReferenceFinder
    {
        internal static TypeReference GetTypeReference(this ModuleDefinition moduleDefinition, Type type, string netCoreAssemblyHint = null)
        {
            var importedType = moduleDefinition.ImportReference(type);
            // On .NET Core, we need to rewrite mscorlib types to use the
            // dot net assemblies from the weaved assembly and not the ones
            // used by the weaver itself.
            if (importedType is TypeSpecification)
                return importedType;

            var scope = importedType.Scope;
            if (scope.Name != moduleDefinition.TypeSystem.CoreLibrary.Name)
                scope = moduleDefinition.TypeSystem.CoreLibrary;

            if (scope.Name == "System.Runtime" && netCoreAssemblyHint != null)
                scope = new AssemblyNameReference(netCoreAssemblyHint,
                    moduleDefinition.AssemblyReferences.First(mr => mr.Name == "System.Runtime").Version);

            importedType.Scope = scope;
            return importedType;
        }
    }
}