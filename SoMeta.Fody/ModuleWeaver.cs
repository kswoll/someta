using System.Collections.Generic;
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

            var interceptorInterface = ModuleDefinition.FindType("SoMeta", "InterceptorAttribute", soMeta);
            var propertyGetInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IPropertyGetInterceptor", soMeta);
            var propertySetInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IPropertySetInterceptor", soMeta);
            var methodInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IMethodInterceptor", soMeta);
            var asyncMethodInterceptorInterface = ModuleDefinition.FindType("SoMeta", "IAsyncMethodInterceptor", soMeta);
            var asyncInvoker = ModuleDefinition.FindType("SoMeta.Helpers", "AsyncInvoker", soMeta);
            var asyncInvokerWrap = ModuleDefinition.FindMethod(asyncInvoker, "Wrap");
            var asyncInvokerUnwrap = ModuleDefinition.FindMethod(asyncInvoker, "Unwrap");

            var propertyGetInterceptions = new List<(PropertyDefinition, CustomAttribute)>();
            var propertySetInterceptions = new List<(PropertyDefinition, CustomAttribute)>();
            var methodInterceptions = new List<(MethodDefinition, CustomAttribute)>();
            var asyncMethodInterceptions = new List<(MethodDefinition, CustomAttribute)>();

            var propertyGetInterceptorWeaver = new PropertyGetInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertyGetInterceptorInterface);
            var propertySetInterceptorWeaver = new PropertySetInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, propertySetInterceptorInterface);
            var methodInterceptorWeaver = new MethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, methodInterceptorInterface);
            var asyncMethodInterceptorWeaver = new AsyncMethodInterceptorWeaver(ModuleDefinition, CecilExtensions.Context, TypeSystem, LogInfo, LogError, LogWarning, asyncMethodInterceptorInterface, asyncInvokerWrap, asyncInvokerUnwrap);

            // Inventory candidate classes
            foreach (var type in ModuleDefinition.GetAllTypes())
            {
                foreach (var property in type.Properties)
                {
                    var getInterceptor = property.GetCustomAttributesInAncestry(propertyGetInterceptorInterface).SingleOrDefault();
                    if (getInterceptor != null)
                    {
                        LogInfo($"Discovered property get interceptor {getInterceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                        propertyGetInterceptions.Add((property, getInterceptor));
                    }
                    var setInterceptor = property.GetCustomAttributesInAncestry(propertySetInterceptorInterface).SingleOrDefault();
                    if (setInterceptor != null)
                    {
                        LogInfo($"Discovered property set interceptor {getInterceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                        propertySetInterceptions.Add((property, setInterceptor));
                    }
                }
                foreach (var method in type.Methods)
                {
                    var interceptor = method.GetCustomAttributesInAncestry(interceptorInterface).SingleOrDefault();
                    if (interceptor != null)
                    {
                        if (methodInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            methodInterceptions.Add((method, interceptor));
                        }
                        else
                        {
                            LogInfo($"Discovered async method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            asyncMethodInterceptions.Add((method, interceptor));
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
        }
    }
}