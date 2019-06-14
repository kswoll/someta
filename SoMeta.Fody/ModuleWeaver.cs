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
            var soMeta = ModuleDefinition.FindAssembly("Someta");

            CecilExtensions.LogInfo = LogInfo;
            CecilExtensions.LogWarning = LogWarning;
            CecilExtensions.LogError = LogError;
            CecilExtensions.Initialize(ModuleDefinition, TypeSystem, soMeta);

            var interceptorInterface = ModuleDefinition.FindType("Someta", "IInterceptor", soMeta);
            var stateInterceptorInterface = ModuleDefinition.FindType("Someta", "IStateInterceptor", soMeta);
            var classInterceptorInterface = ModuleDefinition.FindType("Someta", "IClassInterceptor", soMeta);
            var propertyGetInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertyGetInterceptor", soMeta);
            var propertySetInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertySetInterceptor", soMeta);
            var methodInterceptorInterface = ModuleDefinition.FindType("Someta", "IMethodInterceptor", soMeta);
            var asyncMethodInterceptorInterface = ModuleDefinition.FindType("Someta", "IAsyncMethodInterceptor", soMeta);
            var classEnhancerInterface = ModuleDefinition.FindType("Someta", "IClassEnhancer", soMeta);
            var asyncInvoker = ModuleDefinition.FindType("Someta.Helpers", "AsyncInvoker", soMeta);
            var asyncInvokerWrap = ModuleDefinition.FindMethod(asyncInvoker, "Wrap");
            var asyncInvokerUnwrap = ModuleDefinition.FindMethod(asyncInvoker, "Unwrap");
            var instanceInitializerInterface = ModuleDefinition.FindType("Someta", "IInstanceInitializer", soMeta);
            var interceptorScopeAttribute = ModuleDefinition.FindType("Someta", "InterceptorScopeAttribute", soMeta);
            var requireScopeInterceptorInterface = ModuleDefinition.FindType("Someta", "IRequireScopeInterceptor", soMeta);

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

            bool IsMissingScope(InterceptorAttribute interceptor, out InterceptorScope interceptorScope)
            {
                interceptorScope = interceptor.AttributeType.Resolve().GetCustomAttributeConstructorValue<InterceptorScope>(interceptorScopeAttribute, 0);
                if (requireScopeInterceptorInterface.IsAssignableFrom(interceptor.AttributeType) && interceptorScope == InterceptorScope.None)
                {
                    LogError($"Found an interceptor {interceptor.AttributeType.FullName} at {interceptor.DeclaringType.FullName} that requires scope without an [InterceptorScope] defined");
                    return true;
                }
                else
                {
                    return false;
                }
            }

//            Debugger.Launch();

            // Inventory candidate classes
            foreach (var type in ModuleDefinition.GetAllTypes())
            {
                var classInterceptors = type
                    .GetCustomAttributesInAncestry(interceptorInterface)
                    .Select(x => new InterceptorAttribute(x.DeclaringType, x.Attribute, x.DeclaringType.CustomAttributes.IndexOf(x.Attribute), InterceptorScope.Class))
                    .ToArray();

                foreach (var classInterceptor in classInterceptors)
                {
                    if (IsMissingScope(classInterceptor, out var interceptorScope))
                        continue;

                    LogInfo($"Found interceptor {classInterceptor.AttributeType}");
                    if (classInterceptorInterface.IsAssignableFrom(classInterceptor.AttributeType))
                    {
                        LogInfo($"Found class interceptor {classInterceptor.AttributeType}");
                        if (classEnhancerInterface.IsAssignableFrom(classInterceptor.AttributeType))
                        {
                            LogInfo($"Discovered class enhancer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            classEnhancers.Add((type, classInterceptor));
                        }
                        if (stateInterceptorInterface.IsAssignableFrom(classInterceptor.AttributeType) && interceptorScope.HasFlag(InterceptorScope.Class))
                        {
                            LogInfo($"Discovered class state interceptor {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            stateInterceptions.Add((type, classInterceptor));
                        }
                        if (instanceInitializerInterface.IsAssignableFrom(classInterceptor.AttributeType) && (interceptorScope == InterceptorScope.None || interceptorScope == InterceptorScope.Class))
                        {
                            LogInfo($"Discovered instance initializer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((type, classInterceptor));
                        }
                    }
                }

                foreach (var property in type.Properties)
                {
                    var interceptors = property.GetCustomAttributesIncludingSubtypes(interceptorInterface)
                        .Select(x => new InterceptorAttribute(type, x, property.CustomAttributes.IndexOf(x), InterceptorScope.Property))
                        .Concat(classInterceptors);
                    foreach (var interceptor in interceptors)
                    {
                        if (IsMissingScope(interceptor, out var interceptorScope))
                            continue;

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
                        if (stateInterceptorInterface.IsAssignableFrom(interceptor.AttributeType) && interceptorScope.HasFlag(InterceptorScope.Property))
                        {
                            LogInfo($"Discovered property state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            stateInterceptions.Add((property, interceptor));
                        }
                        if (instanceInitializerInterface.IsAssignableFrom(interceptor.AttributeType) && (interceptorScope == InterceptorScope.None || interceptorScope == InterceptorScope.Property))
                        {
                            LogInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((property, interceptor));
                        }
                    }
                }
                foreach (var method in type.Methods.Where(x => !x.IsConstructor))
                {
                    var interceptors = method.GetCustomAttributesIncludingSubtypes(interceptorInterface)
                        .Select(x => new InterceptorAttribute(type, x, method.CustomAttributes.IndexOf(x), InterceptorScope.Method))
                        .Concat(classInterceptors);
                    foreach (var interceptor in interceptors)
                    {
                        if (IsMissingScope(interceptor, out var interceptorScope))
                            continue;

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
                        if (stateInterceptorInterface.IsAssignableFrom(interceptor.AttributeType) && interceptorScope.HasFlag(InterceptorScope.Method))
                        {
                            LogInfo($"Discovered method state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            stateInterceptions.Add((method, interceptor));
                        }
                        if (instanceInitializerInterface.IsAssignableFrom(interceptor.AttributeType) && (interceptorScope == InterceptorScope.None || interceptorScope == InterceptorScope.Method))
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