﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using CallSite = Mono.Cecil.CallSite;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using ICustomAttributeProvider = Mono.Cecil.ICustomAttributeProvider;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using TypeSystem = Fody.TypeSystem;

namespace Someta.Fody
{
    /// <summary>
    /// Lots of utility methods to make writing IL with Fody less painful.
    /// </summary>
    public static class CecilExtensions
    {
        public static ModuleDefinition ModuleDefinition { get; set; }
        public static global::Fody.TypeSystem TypeSystem { get; set; }
        public static WeaverContext Context { get; set; }

        // Will log an MessageImportance.High message to MSBuild. OPTIONAL
        public static Action<string> LogInfo { get; set; }

        // Will log an error message to MSBuild. OPTIONAL
        public static Action<string> LogError { get; set; }

        public static Action<string> LogWarning { get; set; }

        private static TypeDefinition typeType;
        private static TypeReference taskType;
        private static MethodReference getTypeFromRuntimeHandleMethod;
        private static MethodReference typeGetMethod;
        private static MethodReference typeGetMethods;
        private static TypeReference taskTType;
        private static MethodReference taskFromResult;
        private static TypeReference attributeType;
        private static MethodReference attributeGetCustomAttribute;
        private static MethodReference attributeGetCustomAttributes;
        private static MethodReference attributeGetCustomAttributesForAssembly;
        private static MethodReference methodBaseGetCurrentMethod;
        private static MethodReference typeGetProperty;
        private static MethodReference typeGetEvent;
        private static MethodReference typeGetAssembly;

        internal static bool Initialize(ModuleDefinition moduleDefinition, TypeSystem typeSystem, AssemblyNameReference soMeta)
        {
//            Debugger.Launch();

            ModuleDefinition = moduleDefinition;
            TypeSystem = typeSystem;

            var extensionPointRegistry = ModuleDefinition.FindType("Someta.Helpers", "ExtensionPointRegistry", soMeta);
            if (extensionPointRegistry == null || soMeta == null)
            {
                LogWarning("You are using Someta.Fody but have not referenced or defined any interceptors");
                return false;
            }
            var extensionPointRegistryRegister = ModuleDefinition.FindMethod(extensionPointRegistry, "Register");

            typeType = ModuleDefinition.ImportReference(typeof(Type)).Resolve();
            if (typeType == null)
            {
                throw new InvalidOperationException($"System.Type was somehow not found.  Aborting.");
            }

            taskType = ModuleDefinition.ImportReference(typeof(Task));
            getTypeFromRuntimeHandleMethod = ModuleDefinition.ImportReference(typeType.Methods.Single(x => x.Name == "GetTypeFromHandle"));
            typeGetMethods = ModuleDefinition.ImportReference(CaptureFunc<Type, MethodInfo[]>(x => x.GetMethods(default)));
            typeGetMethod = ModuleDefinition.ImportReference(typeType.Methods.Single(x => x.Name == "GetMethod" && x.Parameters.Count == 5));
            typeGetProperty = ModuleDefinition.ImportReference(CaptureFunc<Type, PropertyInfo>(x => x.GetProperty(default, default(BindingFlags))));
            typeGetEvent = ModuleDefinition.ImportReference(CaptureFunc<Type, EventInfo>(x => x.GetEvent(default, default)));
            taskTType = ModuleDefinition.ImportReference(typeof(Task<>));
            taskFromResult = ModuleDefinition.ImportReference(taskType.Resolve().Methods.Single(x => x.Name == "FromResult"));
            attributeType = ModuleDefinition.ImportReference(typeof(Attribute));
            var attributeTypeDefinition = ModuleDefinition.ImportReference(typeof(Attribute)).Resolve();
            var memberInfoType = ModuleDefinition.ImportReference(typeof(MemberInfo));
            attributeGetCustomAttribute = ModuleDefinition.ImportReference(attributeTypeDefinition.Methods.Single(x => x.Name == nameof(Attribute.GetCustomAttribute) && x.Parameters.Count == 2 && x.Parameters[0].ParameterType.CompareTo(memberInfoType)));
            attributeGetCustomAttributes = ModuleDefinition.ImportReference(attributeTypeDefinition.Methods.Single(x => x.Name == nameof(Attribute.GetCustomAttributes) && x.Parameters.Count == 1 && x.Parameters[0].ParameterType.CompareTo(memberInfoType)));
            var assemblyType = ModuleDefinition.ImportReference(typeof(Assembly));
            attributeGetCustomAttributesForAssembly = ModuleDefinition.ImportReference(attributeTypeDefinition.Methods.Single(x => x.Name == nameof(Attribute.GetCustomAttributes) && x.Parameters.Count == 1 && x.Parameters[0].ParameterType.CompareTo(assemblyType)));
            var methodBaseType = ModuleDefinition.ImportReference(typeof(MethodBase));
            methodBaseGetCurrentMethod = ModuleDefinition.FindMethod(methodBaseType, nameof(MethodBase.GetCurrentMethod));
            typeGetAssembly = ModuleDefinition.ImportReference(typeType.Properties.Single(x => x.Name == nameof(Type.Assembly)).GetMethod);

            var func1Type = ModuleDefinition.ImportReference(typeof(Func<>));
            var func2Type = ModuleDefinition.ImportReference(typeof(Func<,>));
            var action1Type = ModuleDefinition.ImportReference(typeof(Action<>));
            var objectArrayType = ModuleDefinition.ImportReference(typeof(object[]));
            var asyncTaskMethodBuilder = ModuleDefinition.ImportReference(typeof(AsyncTaskMethodBuilder<>));
            var originalMethodAtttribute = ModuleDefinition.FindType("Someta.Reflection", "OriginalMethodAttribute", soMeta);
            var originalMethodAttributeConstructor = ModuleDefinition.FindConstructor(originalMethodAtttribute);
            var methodFinder = ModuleDefinition.FindType("Someta.Reflection", "MethodFinder`1", soMeta, "T");
            var findMethod = ModuleDefinition.FindMethod(methodFinder, "FindMethod");
            var findProperty = ModuleDefinition.FindMethod(methodFinder, "FindProperty");
            var methodInfoType = ModuleDefinition.ImportReference(typeof(MethodInfo));
            var propertyInfoType = ModuleDefinition.ImportReference(typeof(PropertyInfo));
            var eventInfoType = ModuleDefinition.ImportReference(typeof(EventInfo));
            var delegateType = ModuleDefinition.ImportReference(typeof(Delegate));

            var context = new WeaverContext
            {
                ModuleDefinition = ModuleDefinition,
                TypeSystem = typeSystem,
                Someta = soMeta,
                LogWarning = LogWarning,
                LogError = LogError,
                LogInfo = LogInfo,
                Action1Type = action1Type,
                Func1Type = func1Type,
                Func2Type = func2Type,
                ObjectArrayType = objectArrayType,
                TaskType = taskType,
                TaskTType = taskTType,
                AsyncTaskMethodBuilder = asyncTaskMethodBuilder,
                DelegateType = delegateType,
                ActionTypes = new List<TypeReference>
                {
                    ModuleDefinition.ImportReference(typeof(Action)),
                    ModuleDefinition.ImportReference(typeof(Action<>)),
                    ModuleDefinition.ImportReference(typeof(Action<,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Action<,,,,,,,,,,,,,,,>))
                },
                FuncTypes = new List<TypeReference>
                {
                    ModuleDefinition.ImportReference(typeof(Func<>)),
                    ModuleDefinition.ImportReference(typeof(Func<,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,,,,,,>)),
                    ModuleDefinition.ImportReference(typeof(Func<,,,,,,,,,,,,,,,,>))
                },
                OriginalMethodAttributeConstructor = originalMethodAttributeConstructor,
                FindMethod = findMethod,
                MethodFinder = methodFinder,
                MethodInfoType = methodInfoType,
                PropertyInfoType = propertyInfoType,
                EventInfoType = eventInfoType,
                ValueType = ModuleDefinition.ImportReference(typeof(ValueType)),
                RegisterExtensionPoint = extensionPointRegistryRegister
            };
            Context = context;

            return true;
        }

        public static AssemblyNameReference FindAssembly(this ModuleDefinition module, string name)
        {
            return module.AssemblyReferences
                .Where(x => x.Name == name)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
        }

        public static void Emit(this MethodBody body, Action<ILProcessor> il)
        {
            il(body.GetILProcessor());
        }

        public static void EmitToStaticConstructor(this TypeDefinition type, Action<ILProcessor> il)
        {
            var staticConstructor = type.GetStaticConstructor();
            if (staticConstructor == null)
            {
                staticConstructor = type.CreateStaticConstructor();
                staticConstructor.Body.GetILProcessor().Emit(OpCodes.Ret);
            }
            staticConstructor.Body.EmitBeforeReturn(il);
        }

        /// <summary>
        /// Emits to all primary constructors (constructors that do not chain to other constructors of the same class).
        /// The emitted code is appended to the end of the constructor.
        /// </summary>
        public static void EmitToConstructor(this TypeDefinition type, Action<ILProcessor> il)
        {
            foreach (var constructor in type.GetConstructors().Where(x => !x.IsStatic))
            {
                if (constructor.Body.Instructions.Count > 1)
                {
                    var potentialConstructorCall = constructor.Body.Instructions[1];    // [0] is loading "this"
                    if (potentialConstructorCall.Operand is MethodReference reference)
                    {
                        var potentialConstructor = reference.Resolve();
                        if (potentialConstructor.Resolve().IsConstructor && potentialConstructor.DeclaringType.CompareTo(type))
                        {
                            // This is not a primary constructor, so skip
                            continue;
                        }
                    }
                }
                constructor.Body.EmitBeforeReturn(il);
            }
        }

        /// <summary>
        /// Emits to all primary constructors (constructors that do not chain to other constructors of the same class).
        /// Emitted code is prepended to the start of the constructor.
        /// </summary>
        public static void EmitToConstructorStart(this TypeDefinition type, Action<ILProcessor> il)
        {
            foreach (var constructor in type.GetConstructors().Where(x => !x.IsStatic))
            {
                if (constructor.Body.Instructions.Count > 1)
                {
                    var potentialConstructorCall = constructor.Body.Instructions[1];    // [0] is loading "this"
                    if (potentialConstructorCall.Operand is MethodReference reference)
                    {
                        var potentialConstructor = reference.Resolve();
                        if (potentialConstructor.Resolve().IsConstructor && potentialConstructor.DeclaringType.CompareTo(type))
                        {
                            // This is not a primary constructor, so skip
                            continue;
                        }
                    }
                }
                constructor.Body.EmitAfterBaseConstructorCall(il);
            }
        }

        public static GenericInstanceMethod MakeGenericMethod(this MethodReference method, params TypeReference[] genericArguments)
        {
            var result = new GenericInstanceMethod(method);
            foreach (var argument in genericArguments)
                result.GenericArguments.Add(argument);
            return result;
        }

        public static IEnumerable<GenericInstanceType> FindGenericInterfaces(this TypeReference type, TypeReference interfaceType)
        {
            foreach (var current in type.Resolve().Interfaces)
            {
                if (current.InterfaceType is GenericInstanceType genericType && current.InterfaceType.Resolve().CompareTo(interfaceType))
                    yield return genericType;
            }
        }

        public static bool IsAssignableFrom(this TypeReference baseType, TypeReference type, Action<string> logger = null)
        {
            logger ??= (x => { });

            if (type.IsGenericParameter)
                return baseType.CompareTo(type);

            var queue = new Queue<TypeReference>();
            queue.Enqueue(type);

            var checkedTypes = new HashSet<string>();

            while (queue.Any())
            {
                var current = queue.Dequeue();
                logger(current.FullName);

                if (baseType.FullName == current.FullName)
                    return true;

                if (current is GenericInstanceType)
                {
                    queue.Enqueue(current.GetElementType());
                }

                var currentTypeDefinition = current.Resolve();
                if (currentTypeDefinition.BaseType != null)
                {
                    if (!checkedTypes.Contains(currentTypeDefinition.BaseType.FullName))
                    {
                        queue.Enqueue(currentTypeDefinition.BaseType);
                        checkedTypes.Add(currentTypeDefinition.BaseType.FullName);
                    }
                }

                foreach (var @interface in currentTypeDefinition.Interfaces)
                {
                    if (!checkedTypes.Contains(@interface.InterfaceType.FullName))
                    {
                        queue.Enqueue(@interface.InterfaceType);
                        checkedTypes.Add(@interface.InterfaceType.FullName);
                    }
                }
            }

            return false;
        }

        public static TypeDefinition GetEarliestAncestorThatDeclares(this TypeDefinition type, TypeReference attributeType)
        {
            var current = type;
            TypeDefinition result = null;
            while (current != null)
            {
                if (current.IsDefined(attributeType))
                {
                    result = current;
                }

                current = current.BaseType?.Resolve();
            }

            return result;
        }

        public static bool IsDefined(this AssemblyDefinition assembly, TypeReference attributeType)
        {
            var typeIsDefined = assembly.HasCustomAttributes && assembly.CustomAttributes.Any(x => x.AttributeType.FullName == attributeType.FullName);
            return typeIsDefined;
        }

        public static T GetCustomAttributeConstructorValue<T>(this ICustomAttributeProvider declaration, TypeReference attributeType, int argumentIndex)
        {
            var attribute = declaration.CustomAttributes.SingleOrDefault(x => attributeType.IsAssignableFrom(x.AttributeType));
            if (attribute == null)
                return default;

            var value = attribute.ConstructorArguments[argumentIndex].Value;
            return (T)value;
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributes(this AssemblyDefinition assembly, TypeReference attributeType)
        {
            return assembly.CustomAttributes.Where(x => x.AttributeType.FullName == attributeType.FullName);
        }

        public static bool IsDefined(this ModuleDefinition module, TypeReference attributeType)
        {
            var typeIsDefined = module.HasCustomAttributes && module.CustomAttributes.Any(x => x.AttributeType.FullName == attributeType.FullName);
            return typeIsDefined;
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributes(this ModuleDefinition module, TypeReference attributeType)
        {
            return module.CustomAttributes.Where(x => x.AttributeType.FullName == attributeType.FullName);
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributesIncludingSubtypes(this ModuleDefinition module, TypeReference attributeType)
        {
            return module.CustomAttributes.Where(x => attributeType.IsAssignableFrom(x.AttributeType));
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributes(this PropertyDefinition property, TypeReference attributeType)
        {
            return property.CustomAttributes.Where(x => x.AttributeType.FullName == attributeType.FullName);
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributesIncludingSubtypes(this ICustomAttributeProvider member, TypeReference attributeType)
        {
            return member.CustomAttributes.Where(x => attributeType.IsAssignableFrom(x.AttributeType));
        }

        public static IEnumerable<(TypeDefinition DeclaringType, CustomAttribute Attribute)> GetCustomAttributesInAncestry(this TypeDefinition type, TypeReference attributeType)
        {
            var result = type.CustomAttributes.Where(x => attributeType.IsAssignableFrom(x.AttributeType)).Select(x => (type, x));
            if (type.BaseType != null)
                result = result.Concat(type.BaseType.Resolve().GetCustomAttributesInAncestry(attributeType));
            return result;
        }

        public static bool IsDefined(this IMemberDefinition member, TypeReference attributeType, bool inherit = false)
        {
            var typeIsDefined = member.HasCustomAttributes && member.CustomAttributes.Any(x => x.AttributeType.FullName == attributeType.FullName);
            if (!typeIsDefined && inherit && member.DeclaringType?.BaseType != null)
            {
                typeIsDefined = member.DeclaringType.BaseType.Resolve().IsDefined(attributeType, true);
            }

            return typeIsDefined;
        }

        public static bool IsDefinedInAncestry(this IMemberDefinition member, TypeReference attributeType, bool inherit = false)
        {
            var typeIsDefined = member.HasCustomAttributes && member.CustomAttributes.Any(x => attributeType.IsAssignableFrom(x.AttributeType));
            if (!typeIsDefined && inherit && member.DeclaringType?.BaseType != null)
            {
                typeIsDefined = member.DeclaringType.BaseType.Resolve().IsDefined(attributeType, true);
            }

            return typeIsDefined;
        }

        public static FieldReference Bind(this FieldReference field, GenericInstanceType genericType)
        {
            var reference = new FieldReference(field.Name, field.FieldType, genericType);
            return reference;
        }

        public static MethodReference Bind(this MethodReference method, GenericInstanceType genericType)
        {
            var reference = new MethodReference(method.Name, method.ReturnType, genericType)
            {
                HasThis = method.HasThis,
                ExplicitThis = method.ExplicitThis,
                CallingConvention = method.CallingConvention
            };

            foreach (var parameter in method.Parameters)
                reference.Parameters.Add(new ParameterDefinition(ModuleDefinition.ImportReference(parameter.ParameterType)));

            return reference;
        }

        public static MethodReference BindMethod(this MethodReference method, TypeReference genericType, TypeReference[] genericArguments)
        {
            var reference = new MethodReference(method.Name, method.ReturnType, genericType)
            {
                HasThis = method.HasThis,
                ExplicitThis = method.ExplicitThis,
                CallingConvention = method.CallingConvention
            };

            foreach (var parameter in method.Parameters)
                reference.Parameters.Add(new ParameterDefinition(ModuleDefinition.ImportReference(parameter.ParameterType)));

            if (method.HasGenericParameters)
            {
                foreach (var parameter in method.GenericParameters)
                {
                    reference.GenericParameters.Add(new GenericParameter(parameter.Name + "_2", method));
                }

                var result = new GenericInstanceMethod(reference);
                foreach (var argument in genericArguments)
                    result.GenericArguments.Add(argument);
                reference = result;
            }

            return reference;
        }

        public static MethodReference Bind(this MethodReference method)
        {
            var result = method;
            if (method.DeclaringType.HasGenericParameters)
            {
                var genericType = method.DeclaringType.MakeGenericInstanceType(method.DeclaringType.GenericParameters/*.Concat(method.GenericParameters)*/.ToArray());
                result = result.Bind(genericType);
            }

            return result;
        }

        public static FieldReference Bind(this FieldReference field)
        {
            var result = field;
            if (field.DeclaringType.HasGenericParameters)
            {
                var genericType = field.DeclaringType.MakeGenericInstanceType(field.DeclaringType.GenericParameters.ToArray());
                result = result.Bind(genericType);
            }

            return result;
        }

        public static GenericInstanceType MakeGenericNestedType(this TypeReference self, params TypeReference[] arguments)
        {
            if (self == null)
                throw new ArgumentNullException(nameof(self));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            if (arguments.Length == 0)
                throw new ArgumentException();

            var genericInstanceType = new GenericInstanceType(self);
            foreach (TypeReference typeReference in arguments)
                genericInstanceType.GenericArguments.Add(typeReference);
            return genericInstanceType;
        }

        public static FieldReference BindDefinition(this FieldReference field, TypeReference genericTypeDefinition)
        {
            if (!genericTypeDefinition.HasGenericParameters)
                return field;

            var genericDeclaration = new GenericInstanceType(genericTypeDefinition);
            foreach (var parameter in genericTypeDefinition.GenericParameters)
            {
                genericDeclaration.GenericArguments.Add(parameter);
            }

            var reference = new FieldReference(field.Name, field.FieldType, genericDeclaration);
            return reference;
        }

        public static TypeReference FindType(this ModuleDefinition currentModule, string @namespace, string typeName, IMetadataScope scope = null, params string[] typeParameters)
        {
            var result = new TypeReference(@namespace, typeName, currentModule, scope);
            foreach (var typeParameter in typeParameters)
            {
                result.GenericParameters.Add(new GenericParameter(typeParameter, result));
            }

            return currentModule.ImportReference(result);
        }

        public static MethodReference FindConstructor(this ModuleDefinition currentModule, TypeReference type)
        {
            return currentModule.ImportReference(type.Resolve().GetConstructors().Single());
        }

        public static MethodReference FindGetter(this ModuleDefinition currentModule, TypeReference type, string propertyName)
        {
            return currentModule.ImportReference(type.Resolve().Properties.Single(x => x.Name == propertyName).GetMethod);
        }

        public static MethodReference FindSetter(this ModuleDefinition currentModule, TypeReference type, string propertyName)
        {
            return currentModule.ImportReference(type.Resolve().Properties.Single(x => x.Name == propertyName).SetMethod);
        }

        public static MethodReference FindMethod(this ModuleDefinition currentModule, TypeReference type, string name)
        {
            return currentModule.ImportReference(type.Resolve().Methods.Single(x => x.Name == name));
        }

        public static void LoadCurrentMethodInfo(this ILProcessor il)
        {
            il.Emit(OpCodes.Call, methodBaseGetCurrentMethod);
            il.Emit(OpCodes.Castclass, Context.MethodInfoType);
        }

        public static void EmitBeforeReturn(this MethodBody body, Action<ILProcessor> il)
        {
            var retIndex = body.Instructions.Count - 1;
            var ret = body.Instructions[retIndex];

            if (ret.OpCode != OpCodes.Ret)
                throw new InvalidOperationException("Last instruction is not a return instruction, which is illegal");
            if (!body.Method.ReturnType.CompareTo(TypeSystem.VoidReference))
                throw new InvalidOperationException("Method return type is not void, so we cannot insert before the ret statement");

            // Remove the ret and add it back after
            body.Instructions.RemoveAt(retIndex);

            var ilProcessor = body.GetILProcessor();
            il(ilProcessor);

            ilProcessor.Emit(OpCodes.Ret);
        }

        public static Instruction FindConstructorCall(this MethodBody body)
        {
            // Find instruction that call the base constructor
            foreach (var instruction in body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference methodReference)
                {
                    var potentialConstructor = methodReference.Resolve();
                    if (potentialConstructor.IsConstructor)
                    {
                        return instruction;
                    }
                }
            }
            return null;
        }

        public static void EmitAfterBaseConstructorCall(this MethodBody body, Action<ILProcessor> il)
        {
            var constructorCall = body.FindConstructorCall();
            if (constructorCall == null)
                throw new InvalidOperationException($"No constructor call found in method.  Either the method isn't a constructor, or it's badly formed.");
            var startIndex = body.Instructions.IndexOf(constructorCall);

            var instructionsCopy = body.Instructions.ToArray();
            body.Instructions.Clear();

            // Add back the instructions before and including the constructor call
            for (int i = 0; i <= startIndex; i++)
                body.Instructions.Add(instructionsCopy[i]);

            // Add the new instructions
            var ilProcessor = body.GetILProcessor();
            il(ilProcessor);

            // Add back the instructions after the constructor call
            for (int i = startIndex + 1; i < instructionsCopy.Length; i++)
                body.Instructions.Add(instructionsCopy[i]);
        }

        public static void EmitGetPropertyInfo(this ILProcessor il, PropertyDefinition property)
        {
            var bindingFlags = (int)(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            il.LoadType(property.DeclaringType);
            il.Emit(OpCodes.Ldstr, property.Name);
            il.Emit(OpCodes.Ldc_I4, bindingFlags);
            il.EmitCall(typeGetProperty);
        }

        public static void EmitGetEventInfo(this ILProcessor il, EventDefinition @event)
        {
            var bindingFlags = (int)(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            il.LoadType(@event.DeclaringType);
            il.Emit(OpCodes.Ldstr, @event.Name);
            il.Emit(OpCodes.Ldc_I4, bindingFlags);
            il.EmitCall(typeGetEvent);
        }

        public static void EmitGetMethodInfo(this ILProcessor il, MethodDefinition method)
        {
            TypeReference genericType = method.DeclaringType;
            if (genericType.HasGenericParameters)
            {
                genericType = genericType.MakeGenericInstanceType(genericType.GenericParameters.ToArray());
            }

            var methodFinder = Context.MethodFinder.MakeGenericInstanceType(genericType);
            var methodSignature = method.GenerateFullSignature();
            var findMethod = Context.FindMethod.Bind(methodFinder);
            il.Emit(OpCodes.Ldstr, methodSignature);
            il.Emit(OpCodes.Call, findMethod);
        }

        public static void EmitGetAttributeByIndex(this ILProcessor il, FieldReference field, int index, TypeReference attributeType)
        {
            il.EmitGetAttributeByIndex(() => il.LoadField(field), index, attributeType);
        }

        public static void EmitGetAttributeByIndex(this ILProcessor il, TypeDefinition type, int index, TypeReference attributeType)
        {
            il.EmitGetAttributeByIndex(() => il.LoadType(type), index, attributeType);
        }

        public static void EmitGetAssemblyAttributeByIndex(this ILProcessor il, int index, TypeReference attributeType)
        {
            il.LoadTypeAssembly(Context.AssemblyState);
            il.Emit(OpCodes.Call, attributeGetCustomAttributesForAssembly);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldelem_Any, CecilExtensions.attributeType);
            il.Emit(OpCodes.Castclass, attributeType);
        }

        private static void EmitGetAttributeByIndex(this ILProcessor il, Action emitTarget, int index, TypeReference attributeType)
        {
            emitTarget();
            il.Emit(OpCodes.Call, attributeGetCustomAttributes);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldelem_Any, CecilExtensions.attributeType);
            il.Emit(OpCodes.Castclass, attributeType);
        }

        public static void EmitGetAttribute(this ILProcessor il, FieldReference memberInfo, TypeReference attributeType)
        {
            il.Emit(OpCodes.Ldsfld, memberInfo);
            il.LoadType(attributeType);

            il.Emit(OpCodes.Call, attributeGetCustomAttribute);
            il.Emit(OpCodes.Castclass, attributeType);
        }

        public static void EmitGetAttributeFromCurrentMethod(this ILProcessor il, TypeReference attributeType)
        {
            il.LoadCurrentMethodInfo();
            il.LoadType(attributeType);

            il.Emit(OpCodes.Call, attributeGetCustomAttribute);
            il.Emit(OpCodes.Castclass, attributeType);
        }

        public static void EmitGetAttributeFromClass(this ILProcessor il, TypeDefinition type, TypeReference attributeType)
        {
            il.LoadType(type);
            il.LoadType(attributeType);

            il.Emit(OpCodes.Call, attributeGetCustomAttribute);
            il.Emit(OpCodes.Castclass, attributeType);
        }

        /// <summary>
        /// If the specified type is a value type or a generic parameter, this will box the value on
        /// the stack (turning it into an object)
        /// </summary>
        public static void EmitBoxIfNeeded(this ILProcessor il, TypeReference type)
        {
            if (type.IsValueType || type.IsGenericParameter)
                il.Emit(OpCodes.Box, Import(type));
        }

        public static void EmitUnboxIfNeeded(this ILProcessor il, TypeReference type, TypeReference declaringType)
        {
            // If it's a value type, unbox it
            if (type.IsValueType || type.IsGenericParameter)
                il.Emit(OpCodes.Unbox_Any, type.ResolveGenericParameter(declaringType).Import());
            // Otherwise, cast it
            else
                il.Emit(OpCodes.Castclass, type.ResolveGenericParameter(declaringType).Import());
        }

        public static void EmitThisIfRequired(this ILProcessor il, MethodReference method)
        {
            if (!method.Resolve().IsStatic)
                il.Emit(OpCodes.Ldarg_0);
        }

        public static void EmitCall(this ILProcessor il, MethodReference method)
        {
            il.Emit(!method.HasThis ? OpCodes.Call : OpCodes.Callvirt, method.Bind());
        }

        public static void LoadField(this ILProcessor il, FieldReference field)
        {
            il.Emit(field.Resolve().IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field.Bind());
        }

        public static void SaveField(this ILProcessor il, FieldReference field)
        {
            il.Emit(field.Resolve().IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field.Bind());
        }

        public static void EmitStruct(this ILProcessor il, TypeReference type, MethodReference constructor = null, Action emitArgs = null)
        {
            if (constructor == null)
            {
                var local = new VariableDefinition(type);
                il.Body.Variables.Add(local);
                il.Emit(OpCodes.Ldloca_S, local);
                il.Emit(OpCodes.Initobj, type);
                il.Emit(OpCodes.Ldloc, local);
            }
            else
            {
                emitArgs();
                il.Emit(OpCodes.Newobj, constructor);
            }
        }

        /// <summary>
        /// Used when you want to get an argument at a specified index irrespective of whether the surrounding
        /// method is static or not.
        /// </summary>
        public static void EmitArgument(this ILProcessor il, MethodDefinition containingMethod, int argumentIndex)
        {
            int index = argumentIndex;
            if (!containingMethod.IsStatic)
                index++;

            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    il.Emit(OpCodes.Ldarg, index);
                    break;
            }
        }

        public static MethodReference BindAll(this MethodReference method, TypeDefinition declaringType)
        {
            MethodReference result = method;
            if (declaringType.HasGenericParameters)
                result = method.Bind(declaringType.MakeGenericInstanceType(declaringType.GenericParameters.ToArray()));
            var proceedTargetMethod = result.Import();
            var genericProceedTargetMethod = proceedTargetMethod;
            if (method.GenericParameters.Count > 0)
                genericProceedTargetMethod = genericProceedTargetMethod.MakeGenericMethod(method.GenericParameters.Select(x => x.ResolveGenericParameter(declaringType)).ToArray());

            return genericProceedTargetMethod;
        }

        public static MethodReference BindAll(this MethodReference method, TypeReference declaringType, MethodDefinition callerMethod)
        {
            MethodReference result = method;
            if (declaringType.HasGenericParameters)
                result = method.Bind(declaringType.MakeGenericInstanceType(declaringType.GenericParameters.ToArray()));
            var proceedTargetMethod = result.Import();
            var genericProceedTargetMethod = proceedTargetMethod;
            if (method.GenericParameters.Count > 0)
                genericProceedTargetMethod = genericProceedTargetMethod.MakeGenericMethod(callerMethod.GenericParameters.ToArray());

            return genericProceedTargetMethod;
        }

        public static void EmitDelegate(this ILProcessor il, MethodReference handler, TypeReference delegateType, params TypeReference[] typeArguments)
        {
            var proceedDelegateType = delegateType.MakeGenericInstanceType(typeArguments);
            var proceedDelegateTypeConstructor = delegateType.Resolve().GetConstructors().First().Bind(proceedDelegateType);
            il.Emit(OpCodes.Ldftn, handler);
            il.Emit(OpCodes.Newobj, proceedDelegateTypeConstructor);
        }

        public static void EmitLocalMethodDelegate(this ILProcessor il, MethodReference handler, TypeReference delegateType, params TypeReference[] typeArguments)
        {
            // Creating a new delegate expects "this" to be on the stack (for instance methods)
            if (handler.HasThis)
                il.Emit(OpCodes.Ldarg_0);
            // ...and "null" on the stack for static methods
            else
                il.Emit(OpCodes.Ldnull);
            il.EmitDelegate(handler, delegateType, typeArguments);
        }

        public static FieldDefinition CacheMemberInfo(this IMemberDefinition memberDefinition)
        {
            if (memberDefinition is MethodDefinition methodDefinition)
            {
                return CacheMethodInfo(methodDefinition);
            }
            else if (memberDefinition is PropertyDefinition propertyDefinition)
            {
                return CachePropertyInfo(propertyDefinition);
            }
            else if (memberDefinition is EventDefinition eventDefinition)
            {
                return CacheEventInfo(eventDefinition);
            }
            else
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Declares a static field to store the PropertyInfo for the specified PropertyDefinition and initializes
        /// it in the declaring class' static initializer.  If no static initializer currently exists, one will
        /// be created.
        /// </summary>
        public static FieldDefinition CachePropertyInfo(this PropertyDefinition property)
        {
            var type = property.DeclaringType;
            var fieldName = $"<{property.Name}>k__PropertyInfo";
            var field = type.Fields.SingleOrDefault(x => x.Name == fieldName);
            if (field != null)
                return field;

            // Add static field for property
            field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Private, Context.PropertyInfoType);
            type.Fields.Add(field);

            type.EmitToStaticConstructor(il =>
            {
                il.EmitGetPropertyInfo(property);
                il.Emit(OpCodes.Stsfld, field.Bind());
            });

            return field;
        }

        /// <summary>
        /// Declares a static field to store the EventInfo for the specified EventDefinition and initializes
        /// it in the declaring class' static initializer.  If no static initializer currently exists, one will
        /// be created.
        /// </summary>
        public static FieldDefinition CacheEventInfo(this EventDefinition @event)
        {
            var type = @event.DeclaringType;
            var fieldName = $"<{@event.Name}>k__EventInfo";
            var field = type.Fields.SingleOrDefault(x => x.Name == fieldName);
            if (field != null)
                return field;

            // Add static field for property
            field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Private, Context.EventInfoType);
            type.Fields.Add(field);

            type.EmitToStaticConstructor(il =>
            {
                il.EmitGetEventInfo(@event);
                il.Emit(OpCodes.Stsfld, field.Bind());
            });

            return field;
        }

        /// <summary>
        /// Declares a static field to store the MethodInfo for the specified MethodDefinition and initializes
        /// it in the declaring class' static initializer.  If no static initializer currently exists, one will
        /// be created.
        /// </summary>
        public static FieldDefinition CacheMethodInfo(this MethodDefinition method)
        {
            var type = method.DeclaringType;
            var methodSignature = method.GenerateSignature();
            var fieldName = $"<{methodSignature}>k__MethodInfo";
            var field = type.Fields.SingleOrDefault(x => x.Name == fieldName);
            if (field != null)
                return field;

            // Add static field for property
            field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Private, Context.MethodInfoType);
            type.Fields.Add(field);

            type.EmitToStaticConstructor(il =>
            {
                il.EmitGetMethodInfo(method);
                il.Emit(OpCodes.Stsfld, field.Bind());
            });

            return field;
        }

        /// <summary>
        /// Moves the implementation of the specified (original) method into a new method defined in the same class
        /// and returns the new method.  Also, the implementation of the original method is cleared (including debug
        /// state)
        /// </summary>
        public static MethodDefinition MoveImplementation(this MethodDefinition original, string newName)
        {
            var method = new MethodDefinition(newName, MethodAttributes.Private | original.Attributes.GetStatic(), original.ReturnType);
            method.CustomAttributes.Add(new CustomAttribute(Context.OriginalMethodAttributeConstructor)
            {
                ConstructorArguments = { new CustomAttributeArgument(TypeSystem.StringReference, method.Name) }
            });
            original.CopyParameters(method);
            original.CopyGenericParameters(method);

            method.DebugInformation.Scope = original.DebugInformation.Scope;
            method.DebugInformation.StateMachineKickOffMethod = original.DebugInformation.StateMachineKickOffMethod;
            foreach (var sequencePoint in original.DebugInformation.SequencePoints)
            {
                method.DebugInformation.SequencePoints.Add(sequencePoint);
            }
            method.Body = new MethodBody(method);
            foreach (var variable in original.Body.Variables)
            {
                method.Body.InitLocals = true;
                method.Body.Variables.Add(new VariableDefinition(variable.VariableType));
            }
            foreach (var handler in original.Body.ExceptionHandlers)
            {
                method.Body.ExceptionHandlers.Add(handler);
            }
            method.Body.Emit(il =>
            {
                foreach (var instruction in original.Body.Instructions)
                {
                    il.Append(instruction);
                }
            });
            original.DeclaringType.Methods.Add(method);

            // Erase scope since the body is being moved into the $Original method
            original.DebugInformation.Scope = null;
            original.DebugInformation.StateMachineKickOffMethod = null;
            original.DebugInformation.SequencePoints.Clear();

            original.Body = new MethodBody(original)
            {
                InitLocals = true
            };

            return method;
        }

        public static TypeReference Import(this TypeReference type)
        {
            if (type.IsGenericParameter)
                return type;
            else
                return ModuleDefinition.ImportReference(type);
        }

        public static MethodReference Import(this MethodReference method)
        {
            return ModuleDefinition.ImportReference(method);
        }

        public static void EmitDefaultBaseConstructorCall(this ILProcessor il, TypeReference baseType)
        {
            TypeReference constructorType = baseType;
            MethodReference conObj = null;
            while (conObj == null)
            {
                constructorType = (constructorType == null ? baseType : constructorType.Resolve().BaseType) ?? TypeSystem.ObjectReference;
                conObj = ModuleDefinition.ImportReference(constructorType.Resolve().GetConstructors().Single(x => x.Parameters.Count == 0));
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, conObj);
        }

        public static void LoadType(this ILProcessor il, TypeReference type)
        {
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, getTypeFromRuntimeHandleMethod);
        }

        public static void LoadTypeAssembly(this ILProcessor il, TypeReference type)
        {
            il.LoadType(type);
            il.EmitCall(typeGetAssembly);
        }

        public static bool IsTaskT(this TypeReference type)
        {
            var current = type;
            while (current != null)
            {
                if (current is GenericInstanceType genericInstanceType && genericInstanceType.Resolve().GetElementType().CompareTo(taskTType))
                    return true;
                current = current.Resolve().BaseType;
            }

            return false;
        }

        public static bool CompareTo(this TypeReference type, TypeReference compareTo)
        {
            return type.FullName == compareTo.FullName;
        }

        public static TypeReference GetTaskType(this TypeReference type)
        {
            var current = type;
            while (current != null)
            {
                if (current is GenericInstanceType instanceType && instanceType.Resolve().GetElementType().CompareTo(taskTType))
                    return instanceType.GenericArguments.Single();
                current = current.Resolve().BaseType;
            }

            throw new Exception("Type " + type.FullName + " is not an instance of Task<T>");
        }

        public static TypeReference ResolveGenericParameter(this TypeReference genericParameter, TypeReference typeContext)
        {
            if (!genericParameter.IsGenericParameter)
                return genericParameter;

            var name = genericParameter.Name;
            var localParameter = typeContext?.GenericParameters.SingleOrDefault(x => x.Name == name);
            return localParameter ?? genericParameter;
        }

        public static TypeReference ResolveGenericParameterForGenericMethod(this TypeReference genericParameter, TypeReference typeContext, MethodReference method, TypeReference[] genericMethodParameters)
        {
            if (!genericParameter.IsGenericParameter)
                return genericParameter;

            var name = genericParameter.Name;
            TypeReference localParameter = typeContext?.GenericParameters.SingleOrDefault(x => x.Name == name);
            var localMethodParameter = method.GenericParameters.SingleOrDefault(x => x.Name == name);
            if (localMethodParameter != null)
            {
                localParameter = genericMethodParameters[method.GenericParameters.IndexOf(localMethodParameter)];
            }
            return localParameter ?? genericParameter;
        }

        public static void EmitDefaultValue(this ILProcessor il, TypeReference type)
        {
            if (type.CompareTo(TypeSystem.BooleanReference) || type.CompareTo(TypeSystem.ByteReference) ||
                type.CompareTo(TypeSystem.Int16Reference) || type.CompareTo(TypeSystem.Int32Reference))
            {
                il.Emit(OpCodes.Ldc_I4_0);
            }
            else if (type.CompareTo(TypeSystem.SingleReference))
            {
                il.Emit(OpCodes.Ldc_R4, (float)0);
            }
            else if (type.CompareTo(TypeSystem.Int64Reference))
            {
                il.Emit(OpCodes.Ldc_I8);
            }
            else if (type.CompareTo(TypeSystem.DoubleReference))
            {
                il.Emit(OpCodes.Conv_R8);
            }
            else if (type.IsGenericParameter || type.IsValueType)
            {
                var local = new VariableDefinition(type);
                il.Body.Variables.Add(local);
                il.Emit(OpCodes.Ldloca_S, local);
                il.Emit(OpCodes.Initobj, type);
                il.Emit(OpCodes.Ldloc, local);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }
        }

        public static int IndexOf(this IList<Instruction> instructions, Func<Instruction, bool> predicate, int fromIndex = 0)
        {
            for (var i = fromIndex; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (predicate(instruction))
                    return i;
            }

            return -1;
        }

        public static Instruction Clone(this ILProcessor il, Instruction instruction)
        {
            if (instruction.Operand == null)
                return il.Create(instruction.OpCode);
            else if (instruction.Operand is TypeReference operand)
                return il.Create(instruction.OpCode, operand);
            else if (instruction.Operand is FieldReference reference)
                return il.Create(instruction.OpCode, reference);
            else if (instruction.Operand is MethodReference methodReference)
                return il.Create(instruction.OpCode, methodReference);
            else if (instruction.Operand is Instruction instructionOperand)
                return il.Create(instruction.OpCode, instructionOperand);
            else if (instruction.Operand is CallSite site)
                return il.Create(instruction.OpCode, site);
            else if (instruction.Operand is Instruction[] instructions)
                return il.Create(instruction.OpCode, instructions);
            else if (instruction.Operand is ParameterDefinition definition)
                return il.Create(instruction.OpCode, definition);
            else if (instruction.Operand is VariableDefinition variableDefinition)
                return il.Create(instruction.OpCode, variableDefinition);
            else if (instruction.Operand is byte b)
                return il.Create(instruction.OpCode, b);
            else if (instruction.Operand is double d)
                return il.Create(instruction.OpCode, d);
            else if (instruction.Operand is float f)
                return il.Create(instruction.OpCode, f);
            else if (instruction.Operand is int i)
                return il.Create(instruction.OpCode, i);
            else if (instruction.Operand is long l)
                return il.Create(instruction.OpCode, l);
            else if (instruction.Operand is sbyte sb)
                return il.Create(instruction.OpCode, sb);
            else if (instruction.Operand is string s)
                return il.Create(instruction.OpCode, s);
            else
                throw new Exception("Unexpected operand type: " + instruction.Operand.GetType().FullName);
        }

        public static string GenerateSignature(this MethodDefinition method)
        {
            var overloads = method.DeclaringType.Methods.Where(x => x.Name == method.Name).ToList();
            if (overloads.Count == 1)
                return method.Name;

            overloads = overloads.OrderBy(x => x.Parameters.Count).ThenBy(x => string.Join("$", x.Parameters.Select(y => y.ParameterType.FullName))).ToList();
            var index = overloads.IndexOf(method);

            return $"{method.Name}${index}";
        }

        public static string GenerateFullSignature(this MethodDefinition method)
        {
            return $"{method.DeclaringType.FullName}.{method.GenerateSignature()}";
        }

        public static void CopyParameters(this MethodDefinition source, MethodDefinition destination)
        {
            foreach (var parameter in source.Parameters)
            {
                var newParameter = new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType);
                destination.Parameters.Add(newParameter);
            }
        }

        public static void CopyGenericParameters(this IGenericParameterProvider source, IGenericParameterProvider destination, Func<string, string> nameMapper = null)
        {
            if (source.HasGenericParameters)
            {
                foreach (var genericParameter in source.GenericParameters)
                {
                    var newGenericParameter = new GenericParameter(nameMapper?.Invoke(genericParameter.Name) ?? genericParameter.Name, destination);
                    foreach (var constraint in genericParameter.Constraints)
                    {
                        newGenericParameter.Constraints.Add(constraint);
                    }

                    destination.GenericParameters.Add(newGenericParameter);
                }
            }
        }

        public static MethodDefinition CreateStaticConstructor(this TypeDefinition proxyType)
        {
            var constructor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, TypeSystem.VoidReference);
            constructor.Body = new MethodBody(constructor);
            proxyType.Methods.Add(constructor);
            return constructor;
        }

        public static bool IsMethodOverridden(this TypeDefinition type, MethodReference method)
        {
            TypeDefinition currentType = type;
            while (!currentType.CompareTo(method.DeclaringType))
            {
                if (currentType.Methods.SingleOrDefault(x => x.Name == method.Name) != null)
                    return true;
                currentType = currentType.BaseType?.Resolve();
            }

            return false;
        }

        public static MethodInfo CaptureMethod(Expression<Action> expression)
        {
            if (expression.Body is not MethodCallExpression body)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodInfo CaptureMethod<T>(Expression<Action<T>> expression)
        {
            if (expression.Body is not MethodCallExpression body)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodInfo CaptureFunc<T>(Expression<Func<T>> expression)
        {
            if (expression.Body is not MethodCallExpression body)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodInfo CaptureFunc<T, TReturn>(Expression<Func<T, TReturn>> expression)
        {
            if (expression.Body is not MethodCallExpression body)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodAttributes GetStatic(this MethodAttributes attributes)
        {
            return attributes & MethodAttributes.Static;
        }

        public static MethodDefinition CreateMethodThatMatchesStaticScope(this MethodDefinition method, string name, MethodAttributes attributes, TypeReference returnType)
        {
            var type = method.DeclaringType;
            var result = new MethodDefinition(name, attributes | method.Attributes.GetStatic(), returnType);
            result.Body = new MethodBody(result)
            {
                InitLocals = true
            };
            type.Methods.Add(result);
            return result;
        }

        public static string Describe(this MethodDefinition method)
        {
            return $"{method.ReturnType.Name} {method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.Parameters.Select(x => $"{x.ParameterType.Name} {x.Name}"))})";
        }

        public static string Describe(this TypeDefinition type)
        {
            return type.FullName;
        }

        public static string Describe(this PropertyDefinition property)
        {
            return $"{property.DeclaringType.FullName}.{property.Name}";
        }

        public static string Describe(this EventDefinition @event)
        {
            return $"{@event.DeclaringType.FullName}.{@event.Name}";
        }

        public static string Describe(this IMemberDefinition member)
        {
            if (member is MethodDefinition method)
                return Describe(method);
            else if (member is PropertyDefinition property)
                return Describe(property);
            else if (member is EventDefinition @event)
                return Describe(@event);
            else if (member is TypeDefinition type)
                return Describe(type);
            else
                throw new InvalidOperationException($"Unexpected member type: {member.GetType().FullName}");
        }

        private static readonly ConcurrentDictionary<TypeDefinition, HashSet<MethodDefinition>> isPropertyAccessorCache = new();

        public static bool IsPropertyAccessor(this MethodDefinition method)
        {
            var cache = isPropertyAccessorCache.GetOrAdd(method.DeclaringType, x =>
            {
                var properties = method.DeclaringType.Properties;
                var typeCache = new HashSet<MethodDefinition>();
                foreach (var property in properties)
                {
                    if (property.GetMethod != null)
                        typeCache.Add(property.GetMethod);
                    if (property.SetMethod != null)
                        typeCache.Add(property.SetMethod);
                }

                return typeCache;
            });
            return cache.Contains(method);
        }
    }
}