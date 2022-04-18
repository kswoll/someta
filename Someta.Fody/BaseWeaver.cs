using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TypeSystem = Fody.TypeSystem;

namespace Someta.Fody
{
    public class BaseWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }
        public WeaverContext Context { get; set; }
        public TypeSystem TypeSystem { get; set; }
        public Action<string> LogInfo { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string> LogWarning { get; set; }

        private readonly Dictionary<(string, string, string), int> uniqueNamesCounter = new Dictionary<(string, string, string), int>();

        public BaseWeaver(WeaverContext context)
        {
            ModuleDefinition = context.ModuleDefinition;
            Context = context;
            TypeSystem = context.TypeSystem;
            LogInfo = context.LogInfo;
            LogError = context.LogError;
            LogWarning = context.LogWarning;
        }

        public TypeReference FindType(string ns, string name, params string[] typeParameters)
        {
            return ModuleDefinition.FindType(ns, name, Context.Someta, typeParameters);
        }

        public MethodReference FindMethod(TypeReference type, string name)
        {
            return ModuleDefinition.FindMethod(type, name);
        }

        public string GenerateUniqueName(IMemberDefinition member, TypeReference attributeType, string name)
        {
//            Debugger.Launch();
            var key = (member.ToString(), attributeType.FullName, name);
            if (!uniqueNamesCounter.TryGetValue(key, out var counter))
            {
                counter = 0;
            }
            counter++;
            uniqueNamesCounter[key] = counter;

            if (counter > 1)
            {
                name += 2;
            }

            return $"<{(member is TypeDefinition ? "" : member.Name)}>k__{attributeType.Name}{name}";
        }

        public void EmitInstanceArgument(ILProcessor il, MethodDefinition method)
        {
            if (!method.IsStatic)
            {
                // Leave instance (this) on the stack as the second argument
                il.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }
        }

        public void ComposeArgumentsIntoArray(ILProcessor il, MethodDefinition method)
        {
            // Colllect all the parameters into a single array as the third argument
            il.Emit(OpCodes.Ldc_I4, method.Parameters.Count);       // Array length
            il.Emit(OpCodes.Newarr, TypeSystem.ObjectReference);    // Instantiate array
            var startingIndex = method.IsStatic ? 0 : 1;
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                // Duplicate array
                il.Emit(OpCodes.Dup);

                // Array index
                il.Emit(OpCodes.Ldc_I4, i);

                // Element value
                il.Emit(OpCodes.Ldarg, (short)(i + startingIndex));

                var parameter = method.Parameters[i];
                il.EmitBoxIfNeeded(parameter.ParameterType);

                // Set array at index to element value
                il.Emit(OpCodes.Stelem_Any, TypeSystem.ObjectReference);
            }
        }

        public void DecomposeArrayIntoArguments(ILProcessor il, TypeReference declaringType, MethodReference method, bool? isStatic = null)
        {
            // Decompose array into arguments
            isStatic = isStatic ?? !method.HasThis;
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var parameterInfo = method.Parameters[i];
                il.Emit(isStatic.Value ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);                                                    // Push array
                il.Emit(OpCodes.Ldc_I4, i);                                                  // Push element index
                il.Emit(OpCodes.Ldelem_Any, TypeSystem.ObjectReference);                     // Get element
                il.EmitUnboxIfNeeded(parameterInfo.ParameterType, declaringType);
            }
        }

        public FieldDefinition CacheAttributeInstance(IMemberDefinition member, ExtensionPointAttribute extensionPoint)
        {
            FieldDefinition attributeField;
            if (member is TypeDefinition typeDefinition)
            {
                attributeField = CacheAttributeInstance(typeDefinition, extensionPoint);
            }
            else if (member is PropertyDefinition propertyDefinition)
            {
                var propertyInfo = propertyDefinition.CachePropertyInfo();
                attributeField = CacheAttributeInstance(member, propertyInfo, extensionPoint);
            }
            else if (member is MethodDefinition methodDefinition)
            {
                //                Debugger.Launch();
                var methodInfo = methodDefinition.CacheMethodInfo();
                attributeField = CacheAttributeInstance(member, methodInfo, extensionPoint);
            }
            else if (member is EventDefinition eventDefinition)
            {
                var eventInfo = eventDefinition.CacheEventInfo();
                attributeField = CacheAttributeInstance(member, eventInfo, extensionPoint);
            }
            else
            {
                throw new Exception();
            }

            return attributeField;
        }

        public FieldDefinition CacheAttributeInstance(TypeDefinition type, ExtensionPointAttribute extensionPoint)
        {
            return CacheAttributeInstance(type, null, extensionPoint);
        }

        public FieldDefinition CacheAttributeInstance(IMemberDefinition member, FieldDefinition memberInfoField,
            ExtensionPointAttribute extensionPoint)
        {
            if (extensionPoint.Scope == ExtensionPointScope.Assembly)
            {
                var assemblyAttributeFieldName = extensionPoint.AttributeType.FullName.Replace(".", "$");
                var assemblyAttributeField = Context.AssemblyState.Fields.Single(x => x.Name == assemblyAttributeFieldName);
                return assemblyAttributeField;
            }

            var declaringType = extensionPoint.DeclaringType;
            var declaration = extensionPoint.Scope == ExtensionPointScope.Class ? declaringType : member;
            var fieldName = $"<{declaration.Name}>k__{extensionPoint.AttributeType.Name}${extensionPoint.Index}";
            var field = declaringType.Fields.SingleOrDefault(x => x.Name == fieldName);
            if (field != null)
                return field;

            // Add static field for property
            field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Public, extensionPoint.AttributeType);
            declaringType.Fields.Add(field);

            // Since we are explicitly declaring a static constructor, we need to ensure IsBeforeFieldInit is always false, as that being
            // true depends on their not being a static constructor, so if there wasn't a static constructor before the weaving, it'll lead
            // to unexpected results where the static constructor isn't called when expected (like when a type is instantiated).
            if (declaringType.IsBeforeFieldInit)
                declaringType.IsBeforeFieldInit = false;

            declaringType.EmitToStaticConstructor(il =>
            {
                if (extensionPoint.Scope == ExtensionPointScope.Class)
                    il.EmitGetAttributeByIndex(declaringType, extensionPoint.Index, extensionPoint.AttributeType);
                else
                    il.EmitGetAttributeByIndex(memberInfoField, extensionPoint.Index, extensionPoint.AttributeType);
                il.SaveField(field);

                // Register extension point
                if (extensionPoint.Scope == ExtensionPointScope.Class)
                    il.LoadType(declaringType);
                else
                    il.LoadField(memberInfoField);
                il.LoadField(field);
                il.EmitCall(Context.RegisterExtensionPoint);
            });

            return field;
        }
    }
}