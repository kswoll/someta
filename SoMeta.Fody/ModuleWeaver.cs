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
            CecilExtensions.TypeSystem = TypeSystem;
            CecilExtensions.Initialize(ModuleDefinition, soMeta);

            var interceptorInterface = ModuleDefinition.FindType("Someta", "IInterceptor", soMeta);
            var classStateInterceptorInterface = ModuleDefinition.FindType("Someta", "IClassStateInterceptor", soMeta);
            var propertyStateInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertyStateInterceptor", soMeta);
            var methodStateInterceptorInterface = ModuleDefinition.FindType("Someta", "IMethodStateInterceptor", soMeta);
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

            var propertyGetInterceptions = new List<(PropertyDefinition, InterceptorAttribute)>();
            var propertySetInterceptions = new List<(PropertyDefinition, InterceptorAttribute)>();
            var methodInterceptions = new List<(MethodDefinition, InterceptorAttribute)>();
            var asyncMethodInterceptions = new List<(MethodDefinition, InterceptorAttribute)>();
            var classEnhancers = new List<(TypeDefinition, InterceptorAttribute)>();
            var stateInterceptions = new List<(IMemberDefinition, InterceptorAttribute)>();
            var instanceInitializers = new List<(IMemberDefinition, InterceptorAttribute)>();

            var propertyGetInterceptorWeaver = new PropertyGetInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertyGetInterceptorInterface);
            var propertySetInterceptorWeaver = new PropertySetInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertySetInterceptorInterface);
            var methodInterceptorWeaver = new MethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, methodInterceptorInterface);
            var asyncMethodInterceptorWeaver = new AsyncMethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, asyncMethodInterceptorInterface, asyncInvokerWrap, asyncInvokerUnwrap);
            var classEnhancerWeaver = new ClassEnhancerWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning);
            var stateWeaver = new StateWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning);
            var instanceInitializerWeaver = new InstanceInitializerWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning);

            // Inventory candidate classes
            foreach (var type in ModuleDefinition.GetAllTypes())
            {
                var classInterceptors = type
                    .GetCustomAttributesInAncestry(interceptorInterface)
                    .Select(x => new InterceptorAttribute(x.DeclaringType, x.Attribute, x.DeclaringType.CustomAttributes.IndexOf(x.Attribute), InterceptorScope.Class))
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
                        if (classStateInterceptorInterface.IsAssignableFrom(classInterceptor.AttributeType))
                        {
                            LogInfo($"Discovered class state interceptor {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            stateInterceptions.Add((type, classInterceptor));
                        }
                        if (instanceInitializerInterface.IsAssignableFrom(classInterceptor.AttributeType))
                        {
                            LogInfo($"Discovered instance initializer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((type, classInterceptor));
                        }
                    }
                }

                foreach (var property in type.Properties)
                {
                    var interceptors = property.GetCustomAttributesIncludingSubtypes(interceptorInterface)
                        .Select(x => new InterceptorAttribute(type, x, property.CustomAttributes.IndexOf(x), InterceptorScope.Member))
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
                        if (propertyStateInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered property state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            stateInterceptions.Add((property, interceptor));
                        }
                        if (instanceInitializerInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((property, interceptor));
                        }
                    }
                }
                foreach (var method in type.Methods.Where(x => !x.IsConstructor))
                {
                    var interceptors = method.GetCustomAttributesIncludingSubtypes(interceptorInterface)
                        .Select(x => new InterceptorAttribute(type, x, method.CustomAttributes.IndexOf(x), InterceptorScope.Member))
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
                        if (methodStateInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered method state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            stateInterceptions.Add((method, interceptor));
                        }
                        if (instanceInitializerInterface.IsAssignableFrom(interceptor.AttributeType))
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