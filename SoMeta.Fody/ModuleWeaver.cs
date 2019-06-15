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

            var extensionPointInterface = ModuleDefinition.FindType("Someta", "IExtensionPoint", soMeta);
            var stateInterceptorInterface = ModuleDefinition.FindType("Someta", "IStateInterceptor`1", soMeta, "T");
            var stateInterceptorInterfaceBase = ModuleDefinition.FindType("Someta", "IStateInterceptor", soMeta);
            var classExtensionPointInterface = ModuleDefinition.FindType("Someta", "IClassExtensionPoint", soMeta);
            var propertyGetInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertyGetInterceptor", soMeta);
            var propertySetInterceptorInterface = ModuleDefinition.FindType("Someta", "IPropertySetInterceptor", soMeta);
            var eventAddInterceptorInterface = ModuleDefinition.FindType("Someta", "IEventAddInterceptor", soMeta);
            var eventRemoveInterceptorInterface = ModuleDefinition.FindType("Someta", "IEventRemoveInterceptor", soMeta);
            var methodInterceptorInterface = ModuleDefinition.FindType("Someta", "IMethodInterceptor", soMeta);
            var asyncMethodInterceptorInterface = ModuleDefinition.FindType("Someta", "IAsyncMethodInterceptor", soMeta);
            var nonPublicAccessInterface = ModuleDefinition.FindType("Someta", "INonPublicAccess", soMeta);
            var asyncInvoker = ModuleDefinition.FindType("Someta.Helpers", "AsyncInvoker", soMeta);
            var asyncInvokerWrap = ModuleDefinition.FindMethod(asyncInvoker, "Wrap");
            var asyncInvokerUnwrap = ModuleDefinition.FindMethod(asyncInvoker, "Unwrap");
            var instanceInitializerInterfaceBase = ModuleDefinition.FindType("Someta", "IInstanceInitializer", soMeta);
            var instanceInitializerInterface = ModuleDefinition.FindType("Someta", "IInstanceInitializer`1", soMeta, "T");
//            var interceptorScopeAttribute = ModuleDefinition.FindType("Someta", "InterceptorScopeAttribute", soMeta);
//            var requireScopeInterceptorInterface = ModuleDefinition.FindType("Someta", "IRequireScopeInterceptor", soMeta);

            var extensionPointScopesClass = ModuleDefinition.FindType("Someta", "ExtensionPointScopes", soMeta);
            var extensionPointScopesClassDefinition = extensionPointScopesClass.Resolve();
//            var interceptorScopeInterface = interceptorScopesClassDefinition.NestedTypes.Single(x => x.Name == "Scope");
            var extensionPointScopePropertyInterface = extensionPointScopesClassDefinition.NestedTypes.Single(x => x.Name == "Property");
            var extensionPointScopeMethodInterface = extensionPointScopesClassDefinition.NestedTypes.Single(x => x.Name == "Method");
            var extensionPointScopeClassInterface = extensionPointScopesClassDefinition.NestedTypes.Single(x => x.Name == "Class");

            var propertyGetInterceptions = new List<(PropertyDefinition, ExtensionPointAttribute)>();
            var propertySetInterceptions = new List<(PropertyDefinition, ExtensionPointAttribute)>();
            var eventAddInterceptions = new List<(EventDefinition, ExtensionPointAttribute)>();
            var eventRemoveInterceptions = new List<(EventDefinition, ExtensionPointAttribute)>();
            var methodInterceptions = new List<(MethodDefinition, ExtensionPointAttribute)>();
            var asyncMethodInterceptions = new List<(MethodDefinition, ExtensionPointAttribute)>();
            var classEnhancers = new List<(TypeDefinition, ExtensionPointAttribute)>();
            var stateInterceptions = new List<(IMemberDefinition, ExtensionPointAttribute)>();
            var instanceInitializers = new List<(IMemberDefinition, ExtensionPointAttribute)>();

            var propertyGetInterceptorWeaver = new PropertyGetInterceptorWeaver(CecilExtensions.Context, propertyGetInterceptorInterface);
            var propertySetInterceptorWeaver = new PropertySetInterceptorWeaver(CecilExtensions.Context, propertySetInterceptorInterface);
            var eventInterceptorWeaver = new EventInterceptorWeaver(CecilExtensions.Context);
            var methodInterceptorWeaver = new MethodInterceptorWeaver(CecilExtensions.Context, methodInterceptorInterface, asyncMethodInterceptorInterface);
            var asyncMethodInterceptorWeaver = new AsyncMethodInterceptorWeaver(CecilExtensions.Context, asyncMethodInterceptorInterface, asyncInvokerWrap, asyncInvokerUnwrap);
            var classEnhancerWeaver = new ClassEnhancerWeaver(CecilExtensions.Context);
            var stateWeaver = new StateWeaver(CecilExtensions.Context);
            var instanceInitializerWeaver = new InstanceInitializerWeaver(CecilExtensions.Context);

            // unscopedInterface: If present, and if genericTypes is empty (meaning no specific scope was specified),
            // unscopedInterface will be checked as a fallback.
            bool HasScope(ExtensionPointAttribute interceptor, TypeReference interfaceType, ExtensionPointScope scope, TypeReference unscopedInterface = null)
            {
                var genericTypes = interceptor.AttributeType.FindGenericInterfaces(interfaceType).ToArray();
                foreach (var genericType in genericTypes)
                {
                    var argument = genericType.GenericArguments[0];
                    TypeReference scopeInterface = null;
                    switch (scope)
                    {
                        case ExtensionPointScope.Class:
                            scopeInterface = extensionPointScopeClassInterface;
                            break;
                        case ExtensionPointScope.Property:
                            scopeInterface = extensionPointScopePropertyInterface;
                            break;
                        case ExtensionPointScope.Method:
                            scopeInterface = extensionPointScopeMethodInterface;
                            break;
                    }

                    if (argument.CompareTo(scopeInterface))
                        return true;
                }

                // If no scope was specified, we consider the scope satisfied if an unscoped version is satisfied
                // Furthermore, the scope must match the member type.
                var isScopeMatchedWithMember = interceptor.Scope == scope;
                return unscopedInterface != null && genericTypes.Length == 0 && unscopedInterface.IsAssignableFrom(interceptor.AttributeType) && isScopeMatchedWithMember;
            }

            // Inventory candidate classes
            var allTypes = ModuleDefinition.GetAllTypes();
            foreach (var type in allTypes)
            {
                var classInterceptors = type
                    .GetCustomAttributesInAncestry(extensionPointInterface)
                    .Select(x => new ExtensionPointAttribute(x.DeclaringType, x.DeclaringType, x.Attribute, x.DeclaringType.CustomAttributes.IndexOf(x.Attribute), ExtensionPointScope.Class))
                    .ToArray();

                foreach (var classInterceptor in classInterceptors)
                {
                    LogInfo($"Found interceptor {classInterceptor.AttributeType}");
                    if (classExtensionPointInterface.IsAssignableFrom(classInterceptor.AttributeType))
                    {
                        LogInfo($"Found class interceptor {classInterceptor.AttributeType}");
                        if (nonPublicAccessInterface.IsAssignableFrom(classInterceptor.AttributeType))
                        {
                            LogInfo($"Discovered class enhancer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            classEnhancers.Add((type, classInterceptor));
                        }
                        if (HasScope(classInterceptor, stateInterceptorInterface, ExtensionPointScope.Class, stateInterceptorInterfaceBase))
                        {
                            LogInfo($"Discovered class state interceptor {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            stateInterceptions.Add((type, classInterceptor));
                        }
                        if (HasScope(classInterceptor, instanceInitializerInterface, ExtensionPointScope.Class, instanceInitializerInterfaceBase))
                        {
                            LogInfo($"Discovered instance initializer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((type, classInterceptor));
                        }
                    }
                }

                foreach (var property in type.Properties)
                {
                    var interceptors = property.GetCustomAttributesIncludingSubtypes(extensionPointInterface)
                        .Select(x => new ExtensionPointAttribute(type, property, x, property.CustomAttributes.IndexOf(x), ExtensionPointScope.Property))
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
                        if (HasScope(interceptor, stateInterceptorInterface, ExtensionPointScope.Property, stateInterceptorInterfaceBase))
                        {
                            LogInfo($"Discovered property state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            stateInterceptions.Add((property, interceptor));
                        }
                        if (HasScope(interceptor, instanceInitializerInterface, ExtensionPointScope.Property, instanceInitializerInterfaceBase))
                        {
                            LogInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
                            instanceInitializers.Add((property, interceptor));
                        }
                    }
                }

                foreach (var @event in type.Events)
                {
//                    Debugger.Launch();
                    var interceptors = @event.GetCustomAttributesIncludingSubtypes(extensionPointInterface)
                        .Select(x => new ExtensionPointAttribute(type, @event, x, @event.CustomAttributes.IndexOf(x), ExtensionPointScope.Event))
                        .Concat(classInterceptors);

                    foreach (var interceptor in interceptors)
                    {
                        if (eventAddInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered event add interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{@event.Name}");
                            eventAddInterceptions.Add((@event, interceptor));
                        }
                        if (eventRemoveInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            LogInfo($"Discovered event remove interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{@event.Name}");
                            eventRemoveInterceptions.Add((@event, interceptor));
                        }
                    }
                }

                foreach (var method in type.Methods.Where(x => !x.IsConstructor))
                {
                    var interceptors = method.GetCustomAttributesIncludingSubtypes(extensionPointInterface)
                        .Select(x => new ExtensionPointAttribute(type, method, x, method.CustomAttributes.IndexOf(x), ExtensionPointScope.Method))
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
                        if (HasScope(interceptor, stateInterceptorInterface, ExtensionPointScope.Method, stateInterceptorInterfaceBase))
                        {
                            LogInfo($"Discovered method state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            stateInterceptions.Add((method, interceptor));
                        }
                        if (HasScope(interceptor, instanceInitializerInterface, ExtensionPointScope.Method, instanceInitializerInterfaceBase))
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

            foreach (var (@event, interceptor) in eventAddInterceptions)
            {
                eventInterceptorWeaver.Weave(@event, interceptor, isAdd: true);
            }

            foreach (var (@event, interceptor) in eventRemoveInterceptions)
            {
                eventInterceptorWeaver.Weave(@event, interceptor, isAdd: false);
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