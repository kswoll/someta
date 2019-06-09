using System;
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
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace SoMeta.Fody
{
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
        private static TypeReference taskTType;
        private static MethodReference taskFromResult;
        private static MethodReference attributeGetCustomAttribute;
        private static MethodReference methodBaseGetCurrentMethod;
        private static MethodReference typeGetProperty;

        internal static void Initialize(ModuleDefinition moduleDefinition, AssemblyNameReference soMeta)
        {
            ModuleDefinition = moduleDefinition;
            typeType = ModuleDefinition.ImportReference(typeof(Type)).Resolve();
            taskType = ModuleDefinition.ImportReference(typeof(Task));
            getTypeFromRuntimeHandleMethod = ModuleDefinition.ImportReference(typeType.Methods.Single(x => x.Name == "GetTypeFromHandle"));
            typeGetMethod = ModuleDefinition.ImportReference(typeType.Methods.Single(x => x.Name == "GetMethod" && x.Parameters.Count == 5));
            typeGetProperty = ModuleDefinition.ImportReference(CaptureFunc<Type, PropertyInfo>(x => x.GetProperty(default, default(BindingFlags))));
            taskTType = ModuleDefinition.ImportReference(typeof(Task<>));
            taskFromResult = ModuleDefinition.ImportReference(taskType.Resolve().Methods.Single(x => x.Name == "FromResult"));
            var attributeType = ModuleDefinition.ImportReference(typeof(Attribute)).Resolve();
            var memberInfoType = ModuleDefinition.ImportReference(typeof(MemberInfo));
            attributeGetCustomAttribute = ModuleDefinition.ImportReference(attributeType.Methods.Single(x => x.Name == nameof(Attribute.GetCustomAttribute) && x.Parameters.Count == 2 && x.Parameters[0].ParameterType.CompareTo(memberInfoType)));
            var methodBaseType = ModuleDefinition.ImportReference(typeof(MethodBase));
            methodBaseGetCurrentMethod = ModuleDefinition.FindMethod(methodBaseType, nameof(MethodBase.GetCurrentMethod));

            var func2Type = ModuleDefinition.ImportReference(typeof(Func<,>));
            var action1Type = ModuleDefinition.ImportReference(typeof(Action<>));
            var objectArrayType = ModuleDefinition.ImportReference(typeof(object[]));
            var asyncTaskMethodBuilder = ModuleDefinition.ImportReference(typeof(AsyncTaskMethodBuilder<>));
            var originalMethodAttributeConstructor = ModuleDefinition.FindConstructor(ModuleDefinition.FindType("SoMeta.Reflection", "OriginalMethodAttribute", soMeta));
            var methodFinder = ModuleDefinition.FindType("SoMeta.Reflection", "MethodFinder`1", soMeta, "T");
            var findMethod = ModuleDefinition.FindMethod(methodFinder, "FindMethod");
            var findProperty = ModuleDefinition.FindMethod(methodFinder, "FindProperty");
            var methodInfoType = ModuleDefinition.ImportReference(typeof(MethodInfo));
            var propertyInfoType = ModuleDefinition.ImportReference(typeof(PropertyInfo));

            var context = new WeaverContext
            {
                ModuleDefinition = ModuleDefinition,
                LogWarning = LogWarning,
                LogError = LogError,
                LogInfo = LogInfo,
                Action1Type = action1Type,
                Func2Type = func2Type,
                ObjectArrayType = objectArrayType,
                TaskType = taskType,
                AsyncTaskMethodBuilder = asyncTaskMethodBuilder,
                OriginalMethodAttributeConstructor = originalMethodAttributeConstructor,
                FindMethod = findMethod,
                FindProperty = findProperty,
                MethodFinder = methodFinder,
                MethodInfoType = methodInfoType,
                PropertyInfoType = propertyInfoType
            };
            Context = context;
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

        public static GenericInstanceMethod MakeGenericMethod(this MethodReference method, params TypeReference[] genericArguments)
        {
            var result = new GenericInstanceMethod(method);
            foreach (var argument in genericArguments)
                result.GenericArguments.Add(argument);
            return result;
        }

        public static bool IsAssignableFrom(this TypeReference baseType, TypeReference type, Action<string> logger = null)
        {
            if (type.IsGenericParameter)
                return baseType.CompareTo(type);

            return baseType.Resolve().IsAssignableFrom(type.Resolve(), logger);
        }

        public static bool IsAssignableFrom(this TypeDefinition baseType, TypeDefinition type, Action<string> logger = null)
        {
            logger = logger ?? (x => { });

            var queue = new Queue<TypeDefinition>();
            queue.Enqueue(type);

            while (queue.Any())
            {
                var current = queue.Dequeue();
                logger(current.FullName);

                if (baseType.FullName == current.FullName)
                    return true;

                if (current.BaseType != null)
                    queue.Enqueue(current.BaseType.Resolve());

                foreach (var @interface in current.Interfaces)
                {
                    queue.Enqueue(@interface.InterfaceType.Resolve());
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

        public static IEnumerable<CustomAttribute> GetCustomAttributesInAncestry(this ModuleDefinition module, TypeReference attributeType)
        {
            return module.CustomAttributes.Where(x => attributeType.IsAssignableFrom(x.AttributeType));
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributes(this PropertyDefinition property, TypeReference attributeType)
        {
            return property.CustomAttributes.Where(x => x.AttributeType.FullName == attributeType.FullName);
        }

        public static IEnumerable<CustomAttribute> GetCustomAttributesInAncestry(this PropertyDefinition property, TypeReference attributeType)
        {
            return property.CustomAttributes.Where(x => attributeType.IsAssignableFrom(x.AttributeType));
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
            var reference = new MethodReference(method.Name, method.ReturnType, genericType);
            reference.HasThis = method.HasThis;
            reference.ExplicitThis = method.ExplicitThis;
            reference.CallingConvention = method.CallingConvention;

            foreach (var parameter in method.Parameters)
                reference.Parameters.Add(new ParameterDefinition(ModuleDefinition.ImportReference(parameter.ParameterType)));

            return reference;
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

        /*
        public static MethodReference BindDefinition(this MethodReference method, TypeReference genericTypeDefinition)
        {
            if (!genericTypeDefinition.HasGenericParameters)
                return method;

            var genericDeclaration = new GenericInstanceType(genericTypeDefinition);
            foreach (var parameter in genericTypeDefinition.GenericParameters)
            {
                genericDeclaration.GenericArguments.Add(parameter);
            }
            var reference = new MethodReference(method.Name, method.ReturnType, genericDeclaration);
            reference.HasThis = method.HasThis;
            reference.ExplicitThis = method.ExplicitThis;
            reference.CallingConvention = method.CallingConvention;

            foreach (var parameter in method.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            return reference;
        }
        */
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
        }

/*
        public static void EmitGetAttributeFromCurrentMethod(this ILProcessor il, TypeReference attributeType)
        {
            il.LoadCurrentMethodInfo();
            il.LoadType(attributeType);

            il.Emit(OpCodes.Call, attributeGetCustomAttribute);
            il.Emit(OpCodes.Castclass, attributeType);
        }
*/

        public static void EmitBeforeReturn(this MethodBody body, Action<ILProcessor> il)
        {
            var retIndex = body.Instructions.Count - 1;
            var ret = body.Instructions[retIndex];

            if (ret.OpCode != OpCodes.Ret)
                throw new InvalidOperationException("Last instruction is not a return instruction, which is illeagl");
            if (!body.Method.ReturnType.CompareTo(TypeSystem.VoidReference))
                throw new InvalidOperationException("Method return type is not void, so we cannot insert before the ret statement");

            // Remove the ret and add it back after
            body.Instructions.RemoveAt(retIndex);

            var ilProcessor = body.GetILProcessor();
            il(ilProcessor);

            ilProcessor.Emit(OpCodes.Ret);
        }

        public static void EmitGetProperty(this ILProcessor il, PropertyDefinition property)
        {
            var bindingFlags = (int)(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            il.LoadType(property.DeclaringType);
            il.Emit(OpCodes.Ldstr, property.Name);
            il.Emit(OpCodes.Ldc_I4, bindingFlags);
            il.Emit(OpCodes.Call, typeGetProperty);
        }

        public static void EmitGetAttribute(this ILProcessor il, FieldReference memberInfo, TypeReference attributeType)
        {
            il.Emit(OpCodes.Ldsfld, memberInfo);
            il.LoadType(attributeType);

            il.Emit(OpCodes.Call, attributeGetCustomAttribute);
            il.Emit(OpCodes.Castclass, attributeType);
        }

        public static void EmitBoxIfNeeded(this ILProcessor il, TypeReference type)
        {
            if (type.IsValueType || type.IsGenericParameter)
                il.Emit(OpCodes.Box, Import(type));
        }

        /// <summary>
        /// Declares a static field to store the PropertyInfo for the specified PropertyDefinition and initializes
        /// it in the declaring class' static initializer.  If no static initializer currently exists, one will
        /// be created.
        /// </summary>
        public static FieldDefinition CachePropertyInfo(this PropertyDefinition property)
        {
            // Add static field for property
            var type = property.DeclaringType;
            var propertyInfoField = new FieldDefinition($"{property.Name}$PropertyInfo", FieldAttributes.Static | FieldAttributes.Private, Context.PropertyInfoType);
            type.Fields.Add(propertyInfoField);

            var staticConstructor = type.GetStaticConstructor();
            if (staticConstructor == null)
            {
                staticConstructor = type.CreateStaticConstructor();
                staticConstructor.Body.GetILProcessor().Emit(OpCodes.Ret);
            }
            staticConstructor.Body.EmitBeforeReturn(il =>
            {
                il.EmitGetProperty(property);
                il.Emit(OpCodes.Stsfld, propertyInfoField);
            });

            return propertyInfoField;
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

        public static void StoreMethodInfo(this ILProcessor il, FieldReference staticField, TypeReference declaringType, MethodDefinition method)
        {
            var parameterTypes = method.Parameters.Select(info => info.ParameterType).ToArray();

            // The type we want to invoke GetMethod upon
            il.LoadType(declaringType);

            // Arg1: methodName
            il.Emit(OpCodes.Ldstr, method.Name);

            // Arg2: bindingFlags
            il.Emit(OpCodes.Ldc_I4, (int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

            // Arg3: binder
            il.Emit(OpCodes.Ldnull);

            // Arg4: parameterTypes
            il.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
            il.Emit(OpCodes.Newarr, typeType);

            // Copy array for each element we are going to set
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Dup);
            }

            // Set each element
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldc_I4, i);
                il.LoadType(ModuleDefinition.ImportReference(parameterTypes[i]));
                il.Emit(OpCodes.Stelem_Any, typeType);
            }

            // Arg5: parameterModifiers
            il.Emit(OpCodes.Ldnull);

            // Invoke method
            il.Emit(OpCodes.Call, typeGetMethod);

            // Store MethodInfo into the static field
            il.Emit(OpCodes.Stsfld, staticField);
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

        public static TypeReference ResolveGenericParameter(this TypeReference genericParameter, TypeDefinition typeContext)
        {
            if (!genericParameter.IsGenericParameter)
                return genericParameter;

            var name = genericParameter.Name;
            var localParameter = typeContext?.GenericParameters.SingleOrDefault(x => x.Name == name);
            return localParameter ?? genericParameter;
        }

        public static void CreateDefaultMethodImplementation(MethodDefinition methodInfo, ILProcessor il)
        {
            if (taskType.IsAssignableFrom(methodInfo.ReturnType))
            {
                if (methodInfo.ReturnType.IsTaskT())
                {
                    var returnTaskType = methodInfo.ReturnType.GetTaskType();
                    //                    if (returnTaskType.IsGenericParameter)

                    il.EmitDefaultValue(returnTaskType);
                    var fromResult = taskFromResult.MakeGenericMethod(returnTaskType);
                    il.Emit(OpCodes.Call, fromResult);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                    var fromResult = taskFromResult.MakeGenericMethod(TypeSystem.ObjectReference);
                    il.Emit(OpCodes.Call, fromResult);
                }
            }
            else if (!methodInfo.ReturnType.CompareTo(TypeSystem.VoidReference))
            {
                il.EmitDefaultValue(methodInfo.ReturnType.Resolve());
            }

            // Return
            il.Emit(OpCodes.Ret);
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
            return $"{method.Name}$$${method.GenericParameters.Count}$$$" +
                   $"{string.Join("$$", method.Parameters.Select(x => x.ParameterType.FullName.Replace(".", "$")))}";
        }

        public static void CopyParameters(this MethodDefinition source, MethodDefinition destination)
        {
            foreach (var parameter in source.Parameters)
            {
                var newParameter = new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType);
                destination.Parameters.Add(newParameter);
            }
        }

        public static void CopyGenericParameters(this IGenericParameterProvider source, IGenericParameterProvider destination)
        {
            if (source.HasGenericParameters)
            {
                foreach (var genericParameter in source.GenericParameters)
                {
                    var newGenericParameter = new GenericParameter(genericParameter.Name, destination);
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

        /*

                public static IEnumerable<TypeDefinition> GetAllTypes(this ModuleDefinition module)
                {
                    var stack = new Stack<TypeDefinition>();
                    foreach (var type in module.Types)
                    {
                        stack.Push(type);
                    }
                    while (stack.Any())
                    {
                        var current = stack.Pop();
                        yield return current;

                        foreach (var nestedType in current.NestedTypes)
                        {
                            stack.Push(nestedType);
                        }
                    }
                }
        */

        public static MethodInfo CaptureMethod(Expression<Action> expression)
        {
            var body = expression.Body as MethodCallExpression;
            if (body == null)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodInfo CaptureMethod<T>(Expression<Action<T>> expression)
        {
            var body = expression.Body as MethodCallExpression;
            if (body == null)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodInfo CaptureFunc<T>(Expression<Func<T>> expression)
        {
            var body = expression.Body as MethodCallExpression;
            if (body == null)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }

        public static MethodInfo CaptureFunc<T, TReturn>(Expression<Func<T, TReturn>> expression)
        {
            var body = expression.Body as MethodCallExpression;
            if (body == null)
                throw new ArgumentException("Pass in a method call expression", nameof(expression));

            return body.Method;
        }
    }
}