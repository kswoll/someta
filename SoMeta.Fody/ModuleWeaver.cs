using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace SoMeta.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            return new[] { "netstandard", "mscorlib" };
        }

        public override void Execute()
        {
            var soMeta = ModuleDefinition.FindAssembly("SoMeta");

            CecilExtensions.LogInfo = LogInfo;
            CecilExtensions.LogWarning = LogWarning;
            CecilExtensions.LogError = LogError;
            CecilExtensions.TypeSystem = TypeSystem;
            CecilExtensions.Initialize(ModuleDefinition, soMeta);

            var interceptorInterface = ModuleDefinition.FindType("SoMeta", "IInterceptor", soMeta);
            var propertyGetInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IPropertyGetInterceptor", soMeta);
            var propertySetInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IPropertySetInterceptor", soMeta);
            var methodInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IMethodInterceptor", soMeta);
            var asyncMethodInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IAsyncMethodInterceptor", soMeta);
            var classEnhancerInterface = ModuleDefinition.FindType("SoMeta", "IClassEnhancer", soMeta);
            var asyncInvoker = ModuleDefinition.FindType("SoMeta.Helpers", "AsyncInvoker", soMeta);
            var asyncInvokerWrap = ModuleDefinition.FindMethod(asyncInvoker, "Wrap");
            var asyncInvokerUnwrap = ModuleDefinition.FindMethod(asyncInvoker, "Unwrap");

            var propertyGetInterceptions = new List<(PropertyDefinition, CustomAttribute, int, InterceptorScope)>();
            var propertySetInterceptions = new List<(PropertyDefinition, CustomAttribute, int, InterceptorScope)>();
            var methodInterceptions = new List<(MethodDefinition, CustomAttribute, int, InterceptorScope)>();
            var asyncMethodInterceptions = new List<(MethodDefinition, CustomAttribute, int, InterceptorScope)>();
            var classEnhancers = new List<(TypeDefinition, CustomAttribute, int, InterceptorScope)>();

            var propertyGetInterceptorWeaver = new PropertyGetInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertyGetInterceptorInterface);
            var propertySetInterceptorWeaver = new PropertySetInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertySetInterceptorInterface);
            var methodInterceptorWeaver = new MethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, methodInterceptorInterface);
            var asyncMethodInterceptorWeaver = new AsyncMethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, asyncMethodInterceptorInterface, asyncInvokerWrap, asyncInvokerUnwrap);
            var classEnhancerWeaver = new ClassEnhancerWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning);

            // Inventory candidate classes
            foreach (var type in ModuleDefinition.GetAllTypes())
            {
                var classInterceptors = type
                    .GetCustomAttributesInAncestry(interceptorInterface)
                    .Select(x => (x, type.CustomAttributes.IndexOf(x), InterceptorScope.Class))
                    .ToArray();

                foreach (var (interceptor, index, scope) in classInterceptors.Where(x => ))
                {
                    if (classEnhancerInterface.IsAssignableFrom(interceptor.AttributeType))
                    {
                        
                    }
                }

                foreach (var property in type.Properties)
                {
                    var interceptors = property.GetCustomAttributesInAncestry(interceptorInterface)
                        .Select(x => (x, property.CustomAttributes.IndexOf(x), InterceptorScope.Member))
                        .Concat(classInterceptors);
                    foreach (var (interceptor, index, scope) in interceptors)
                    {
                        if (propertyGetInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered property get interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            propertyGetInterceptions.Add((property, interceptor, index, scope));
                        }
                        if (propertySetInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered property set interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            propertySetInterceptions.Add((property, interceptor, index, scope));
                        }
                    }
                }
                foreach (var method in type.Methods.Where(x => !x.IsConstructor))
                {
                    var interceptors = method.GetCustomAttributesInAncestry(interceptorInterface)
                        .Select(x => (x, method.CustomAttributes.IndexOf(x), InterceptorScope.Member))
                        .Concat(classInterceptors);
                    foreach (var (interceptor, index, scope) in interceptors)
                    {
                        if (methodInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            methodInterceptions.Add((method, interceptor, index, scope));
                        }
                        if (asyncMethodInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered async method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            asyncMethodInterceptions.Add((method, interceptor, index, scope));
                        }
                    }
                }
            }

            foreach (var (property, interceptor, index, scope) in propertyGetInterceptions)
            {
                propertyGetInterceptorWeaver.Weave(property, interceptor, index, scope);
            }

            foreach (var (property, interceptor, index, scope) in propertySetInterceptions)
            {
                propertySetInterceptorWeaver.Weave(property, interceptor, index, scope);
            }

            foreach (var (method, interceptor, index, scope) in methodInterceptions)
            {
                methodInterceptorWeaver.Weave(method, interceptor, index, scope);
            }

            foreach (var (method, interceptor, index, scope) in asyncMethodInterceptions)
            {
                asyncMethodInterceptorWeaver.Weave(method, interceptor, index, scope);
            }
        }
    }
}