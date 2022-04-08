using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

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

            CecilExtensions.LogInfo = WriteInfo;
            CecilExtensions.LogWarning = WriteWarning;
            CecilExtensions.LogError = WriteError;
            CecilExtensions.Initialize(ModuleDefinition, TypeSystem, soMeta);

            var extensionPointInterface = ModuleDefinition.FindType("Someta", "IExtensionPoint", soMeta);
            var stateExtensionPointInterface = ModuleDefinition.FindType("Someta", "IStateExtensionPoint`1", soMeta, "T");
            var stateExtensionPointInterfaceBase = ModuleDefinition.FindType("Someta", "IStateExtensionPoint", soMeta);
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
            var assemblyInterceptorAttributes = ModuleDefinition.Assembly
                .GetCustomAttributesIncludingSubtypes(extensionPointInterface)
                .ToArray();
            ExtensionPointAttribute[] assemblyInterceptors;

            // If we have any assembly-level interceptors, we create a special state class to hold the attribute instances (since attributes
            // can contain state, but getting attributes through reflection always returns a new instance.
            if (assemblyInterceptorAttributes.Any())
            {
//                Debugger.Launch();
                var assemblyState = new TypeDefinition("Someta", "AssemblyState", TypeAttributes.Public, TypeSystem.ObjectReference);
                ModuleDefinition.Types.Add(assemblyState);

/*
                var constructorWithTarget = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, TypeSystem.VoidReference);
                constructorWithTarget.Body.Emit(il =>
                {
                    il.Emit(OpCodes.Ret);
                });
                assemblyState.Methods.Add(constructorWithTarget);
*/

                CecilExtensions.Context.AssemblyState = assemblyState;
                assemblyInterceptors = assemblyInterceptorAttributes
                    .Select(x => new ExtensionPointAttribute(assemblyState, ModuleDefinition.Assembly, x, ModuleDefinition.Assembly.CustomAttributes.IndexOf(x), ExtensionPointScope.Assembly))
                    .ToArray();

                foreach (var interceptor in assemblyInterceptors)
                {
                    var fieldName = interceptor.AttributeType.FullName.Replace(".", "$");
                    var attributeField = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Public, interceptor.AttributeType);
                    var index = ModuleDefinition.Assembly.CustomAttributes.IndexOf(interceptor.Attribute);
                    assemblyState.Fields.Add(attributeField);

                    assemblyState.EmitToStaticConstructor(il =>
                    {
                        il.EmitGetAssemblyAttributeByIndex(index, interceptor.AttributeType);
                        il.SaveField(attributeField);
                    });
                }
            }
            else
            {
                assemblyInterceptors = new ExtensionPointAttribute[0];
            }

//            var moduleInterceptors = ModuleDefinition
//                .GetCustomAttributesIncludingSubtypes(extensionPointInterface)
//                .Select(x => new ExtensionPointAttribute(null, ModuleDefinition, x, ModuleDefinition.CustomAttributes.IndexOf(x), ExtensionPointScope.Module))
//                .ToArray();
//            var assemblyAndModuleInterceptors = assemblyInterceptors.Concat(moduleInterceptors).ToArray();
            foreach (var type in allTypes)
            {
                // We can get into recursion scenarios if we allow extension points on extension points.  For now, let's naively prohibit this
                if (extensionPointInterface.IsAssignableFrom(type))
                    continue;

                var classInterceptors = type
                    .GetCustomAttributesInAncestry(extensionPointInterface)
                    .Select(x => new ExtensionPointAttribute(x.DeclaringType, x.DeclaringType, x.Attribute, x.DeclaringType.CustomAttributes.IndexOf(x.Attribute), ExtensionPointScope.Class))
                    .Concat(assemblyInterceptors/*.Select(x => new ExtensionPointAttribute(type, x.DeclaringMember, x.Attribute, x.Index, x.Scope))*/)
                    .ToArray();

                foreach (var classInterceptor in classInterceptors)
                {
                    WriteInfo($"Found class interceptor {classInterceptor.AttributeType}");
                    if (nonPublicAccessInterface.IsAssignableFrom(classInterceptor.AttributeType))
                    {
                        WriteInfo($"Discovered class enhancer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                        classEnhancers.Add((type, classInterceptor));
                    }
                    if (HasScope(classInterceptor, stateExtensionPointInterface, ExtensionPointScope.Class, stateExtensionPointInterfaceBase))
                    {
                        WriteInfo($"Discovered class state interceptor {classInterceptor.AttributeType.FullName} at {type.FullName}");
                        stateInterceptions.Add((type, classInterceptor));
                    }
                    if (HasScope(classInterceptor, instanceInitializerInterface, ExtensionPointScope.Class, instanceInitializerInterfaceBase))
                    {
                        WriteInfo($"Discovered instance initializer {classInterceptor.AttributeType.FullName} at {type.FullName}");
                        instanceInitializers.Add((type, classInterceptor));
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
                            WriteInfo($"Discovered property get interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            propertyGetInterceptions.Add((property, interceptor));
                        }
                        if (propertySetInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            WriteInfo($"Discovered property set interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            propertySetInterceptions.Add((property, interceptor));
                        }
                        if (HasScope(interceptor, stateExtensionPointInterface, ExtensionPointScope.Property, stateExtensionPointInterfaceBase))
                        {
                            WriteInfo($"Discovered property state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");
                            stateInterceptions.Add((property, interceptor));
                        }
                        if (HasScope(interceptor, instanceInitializerInterface, ExtensionPointScope.Property, instanceInitializerInterfaceBase))
                        {
                            WriteInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
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
                            WriteInfo($"Discovered event add interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{@event.Name}");
                            eventAddInterceptions.Add((@event, interceptor));
                        }
                        if (eventRemoveInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            WriteInfo($"Discovered event remove interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{@event.Name}");
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
                            WriteInfo($"Discovered method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            methodInterceptions.Add((method, interceptor));
                        }
                        if (asyncMethodInterceptorInterface.IsAssignableFrom(interceptor.AttributeType))
                        {
                            WriteInfo($"Discovered async method interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            asyncMethodInterceptions.Add((method, interceptor));
                        }
                        if (HasScope(interceptor, stateExtensionPointInterface, ExtensionPointScope.Method, stateExtensionPointInterfaceBase))
                        {
                            WriteInfo($"Discovered method state interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{method.Name}");
                            stateInterceptions.Add((method, interceptor));
                        }
                        if (HasScope(interceptor, instanceInitializerInterface, ExtensionPointScope.Method, instanceInitializerInterfaceBase))
                        {
                            WriteInfo($"Discovered instance initializer {interceptor.AttributeType.FullName} at {type.FullName}");
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