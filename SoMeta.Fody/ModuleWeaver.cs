using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            return new[] { "netstandard", "mscorlib" };
        }

        public override void Execute()
        {
//            Debugger.Launch();

            var soMeta = ModuleDefinition.FindAssembly("Someta");

            CecilExtensions.LogInfo = LogInfo;
            CecilExtensions.LogWarning = LogWarning;
            CecilExtensions.LogError = LogError;
            CecilExtensions.Initialize(ModuleDefinition, TypeSystem, soMeta);

            var interceptorInterface = ModuleDefinition.FindType("Someta", "IInterceptor", soMeta);
            var stateInterceptorInterface = ModuleDefinition.FindType("Someta", "IStateInterceptor`1", soMeta, "T");
            var classInterceptorInterface = ModuleDefinition.FindType("Someta", "IClassInterceptor", soMeta);
            var propertyGetInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertyGetInterceptor", soMeta);
            var propertySetInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertySetInterceptor", soMeta);
            var methodInterceptorInterface = ModuleDefinition.FindType("Someta", "IMethodInterceptor", soMeta);
            var asyncMethodInterceptorInterface = ModuleDefinition.FindType("Someta", "IAsyncMethodInterceptor", soMeta);
            var classEnhancerInterface = ModuleDefinition.FindType("Someta", "IClassEnhancer", soMeta);
            var asyncInvoker = ModuleDefinition.FindType("Someta.Helpers", "AsyncInvoker", soMeta);
            var asyncInvokerWrap = ModuleDefinition.FindMethod(asyncInvoker, "Wrap");
            var asyncInvokerUnwrap = ModuleDefinition.FindMethod(asyncInvoker, "Unwrap");
            var instanceInitializerInterfaceBase = ModuleDefinition.FindType("Someta", "IInstanceInitializer", soMeta);
            var instanceInitializerInterface = ModuleDefinition.FindType("Someta", "IInstanceInitializer`1", soMeta, "T");
//            var interceptorScopeAttribute = ModuleDefinition.FindType("Someta", "InterceptorScopeAttribute", soMeta);
//            var requireScopeInterceptorInterface = ModuleDefinition.FindType("Someta", "IRequireScopeInterceptor", soMeta);

            var interceptorScopesClass = ModuleDefinition.FindType("Someta", "InterceptorScopes", soMeta);
            var interceptorScopesClassDefinition = interceptorScopesClass.Resolve();
//            var interceptorScopeInterface = interceptorScopesClassDefinition.NestedTypes.Single(x => x.Name == "Scope");
            var interceptorScopePropertyInterface = interceptorScopesClassDefinition.NestedTypes.Single(x => x.Name == "Property");
            var interceptorScopeMethodInterface = interceptorScopesClassDefinition.NestedTypes.Single(x => x.Name == "Method");
            var interceptorScopeClassInterface = interceptorScopesClassDefinition.NestedTypes.Single(x => x.Name == "Class");

            var propertyGetInterceptions = new List<(PropertyDefinition, InterceptorAttribute)>();
            var propertySetInterceptions = new List<(PropertyDefinition, InterceptorAttribute)>();
            var methodInterceptions = new List<(MethodDefinition, InterceptorAttribute)>();
            var asyncMethodInterceptions = new List<(MethodDefinition, InterceptorAttribute)>();
            var classEnhancers = new List<(TypeDefinition, InterceptorAttribute)>();
            var stateInterceptions = new List<(IMemberDefinition, InterceptorAttribute)>();
            var instanceInitializers = new List<(IMemberDefinition, InterceptorAttribute)>();

            var propertyGetInterceptorWeaver = new PropertyGetInterceptorWeaver(CecilExtensions.Context, propertyGetInterceptorInterface);
            var propertySetInterceptorWeaver = new PropertySetInterceptorWeaver(CecilExtensions.Context, propertySetInterceptorInterface);
            var methodInterceptorWeaver = new MethodInterceptorWeaver(CecilExtensions.Context, methodInterceptorInterface, asyncMethodInterceptorInterface);
            var asyncMethodInterceptorWeaver = new AsyncMethodInterceptorWeaver(CecilExtensions.Context, asyncMethodInterceptorInterface, asyncInvokerWrap, asyncInvokerUnwrap);
            var classEnhancerWeaver = new ClassEnhancerWeaver(CecilExtensions.Context);
            var stateWeaver = new StateWeaver(CecilExtensions.Context);
            var instanceInitializerWeaver = new InstanceInitializerWeaver(CecilExtensions.Context);

            // unscopedInterface: If present, and if genericTypes is empty (meaning no specific scope was specified),
            // unscopedInterface will be checked as a fallback.
            bool HasScope(InterceptorAttribute interceptor, TypeReference interfaceType, InterceptorScope scope, TypeReference unscopedInterface = null)
            {
                var genericTypes = interceptor.AttributeType.FindGenericInterfaces(interfaceType).ToArray();
                foreach (var genericType in genericTypes)
                {
                    var argument = genericType.GenericArguments[0];
                    TypeReference scopeInterface = null;
                    switch (scope)
                    {
                        case InterceptorScope.Class:
                            scopeInterface = interceptorScopeClassInterface;
                            break;
                        case InterceptorScope.Property:
                            scopeInterface = interceptorScopePropertyInterface;
                            break;
                        case InterceptorScope.Method:
                            scopeInterface = interceptorScopeMethodInterface;
                            break;
                    }

                    if (argument.CompareTo(scopeInterface))
                        return true;
                }

                // If no scope was specified, we consider the scope satisfied if an unscoped version is satisfied
                // Furthermore, the scope must match the member type.
                bool isScopeMatchedWithMember = interceptor.Scope == scope;
/*
                switch (interceptor.Scope)
                {
                    case InterceptorScope.Class:
                        isScopeMatchedWithMember = interceptor.DeclaringMember is TypeDefinition;
                        break;
                    case InterceptorScope.Property:
                        isScopeMatchedWithMember = interceptor.DeclaringMember is PropertyDefinition;
                        break;
                    case InterceptorScope.Method:
                        isScopeMatchedWithMember = interceptor.DeclaringMember is MethodDefinition;
                        break;
                }
*/
                return unscopedInterface != null && genericTypes.Length == 0 && unscopedInterface.IsAssignableFrom(interceptor.AttributeType) && isScopeMatchedWithMember;
            }

            // Inventory candidate classes
            var allTypes = ModuleDefinition.GetAllTypes();
            foreach (var type in allTypes)
            {
                var classInterceptors = type
                    .GetCustomAttributesInAncestry(interceptorInterface)
                    .Select(x => new InterceptorAttribute(x.DeclaringType, x.DeclaringType, x.Attribute, x.DeclaringType.CustomAttributes.IndexOf(x.Attribute), InterceptorScope.Class))
                    .ToArray();

                foreach (var classInterceptor in classInterceptors)
                {
                    LogInfo($"Found interceptor {classInterceptor.AttributeType}");
                    if (classInterceptorInterface.IsAssignableFrom(classInterceptor.AttributeType))
                    {
                        LogInfo($"Found class interceptor {classInterceptor.AttributeType}");
                        if (classEnhancerInterface.IsAssignableFrom(classInterceptor.AttributeType))
                        {
                            LogInfo($"Discovered class enhancer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            classEnhancers.Add((type, classInterceptor));
                        }
                        if (HasScope(classInterceptor, stateInterceptorInterface, InterceptorScope.Class))
                        {
                            LogInfo($"Discovered class state interceptor {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            stateInterceptions.Add((type, classInterceptor));
                        }
                        if (HasScope(classInterceptor, instanceInitializerInterface, InterceptorScope.Class, instanceInitializerInterfaceBase))
                        {
                            LogInfo($"Discovered instance initializer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((type, classInterceptor));
                        }
                    }
                }

                foreach (var property in type.Properties)
                {
                    var interceptors = property.GetCustomAttributesIncludingSubtypes(interceptorInterface)
                        .Select(x => new InterceptorAttribute(type, property, x, property.CustomAttributes.IndexOf(x), InterceptorScope.Property))
                        .Concat(classInterceptors);
                    foreach (var interceptor in interceptors)
                    {
                        if (propertyGetInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered property get interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            propertyGetInterceptions.Add((property, interceptor));
                        }
                        if (propertySetInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered property set interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            propertySetInterceptions.Add((property, interceptor));
                        }
                        if (HasScope(interceptor, stateInterceptorInterface, InterceptorScope.Property))
                        {
                            LogInfo($"Discovered property state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            stateInterceptions.Add((property, interceptor));
                        }
                        if (HasScope(interceptor, instanceInitializerInterface, InterceptorScope.Property, instanceInitializerInterfaceBase))
                        {
                            LogInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((property, interceptor));
                        }
                    }
                }
                foreach (var method in type.Methods.Where(x => !x.IsConstructor))
                {
                    var interceptors = method.GetCustomAttributesIncludingSubtypes(interceptorInterface)
                        .Select(x => new InterceptorAttribute(type, method, x, method.CustomAttributes.IndexOf(x), InterceptorScope.Method))
                        .Concat(classInterceptors);
                    foreach (var interceptor in interceptors)
                    {
                        if (methodInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            methodInterceptions.Add((method, interceptor));
                        }
                        if (asyncMethodInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered async method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            asyncMethodInterceptions.Add((method, interceptor));
                        }
                        if (HasScope(interceptor, stateInterceptorInterface, InterceptorScope.Method))
                        {
                            LogInfo($"Discovered method state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            stateInterceptions.Add((method, interceptor));
                        }
                        if (HasScope(interceptor, instanceInitializerInterface, InterceptorScope.Method, instanceInitializerInterfaceBase))
                        {
                            LogInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((method, interceptor));
                        }
                    }
                }
            }

            foreach (var (property, interceptor) in propertyGetInterceptions)
            {
                propertyGetInterceptorWeaver.Weave(property, interceptor);
            }

            foreach (var (property, interceptor) in propertySetInterceptions)
            {
                propertySetInterceptorWeaver.Weave(property, interceptor);
            }

            foreach (var (method, interceptor) in methodInterceptions)
            {
                methodInterceptorWeaver.Weave(method, interceptor);
            }

            foreach (var (method, interceptor) in asyncMethodInterceptions)
            {
                asyncMethodInterceptorWeaver.Weave(method, interceptor);
            }

            foreach (var (type, interceptor) in classEnhancers)
            {
                classEnhancerWeaver.Weave(type, interceptor);
            }

            foreach (var (member, interceptor) in stateInterceptions)
            {
                stateWeaver.Weave(member, interceptor);
            }

            foreach (var (type, interceptor) in instanceInitializers)
            {
                instanceInitializerWeaver.Weave(type, interceptor);
            }
        }
    }
}