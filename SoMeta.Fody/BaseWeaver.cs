using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class BaseWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }
        public WeaverContext Context { get; set; }
        public TypeSystem TypeSystem { get; set; }
        public Action<string> LogInfo { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string> LogWarning { get; set; }

        public BaseWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning)
        {
            ModuleDefinition = moduleDefinition;
            Context = context;
            TypeSystem = typeSystem;
            LogInfo = logInfo;
            LogError = logError;
            LogWarning = logWarning;
        }

        protected void EmitInstanceArgument(ILProcessor il, MethodDefinition method)
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

        protected void ComposeArgumentsIntoArray(ILProcessor il, MethodDefinition method)
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

        protected void DecomposeArrayIntoArguments(ILProcessor il, MethodDefinition method)
        {
            // Decompose array into arguments
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var parameterInfo = method.Parameters[i];
                il.Emit(method.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);                                                    // Push array
                il.Emit(OpCodes.Ldc_I4, i);                                                  // Push element index
                il.Emit(OpCodes.Ldelem_Any, TypeSystem.ObjectReference);                     // Get element
                il.EmitUnboxIfNeeded(parameterInfo.ParameterType, method.DeclaringType);
            }
        }

        protected void EmitAttribute(ILProcessor il, MethodDefinition method, TypeReference interceptorAttribute, InterceptorScope scope)
        {
            switch (scope)
            {
                case InterceptorScope.Member:
                    il.EmitGetAttributeFromCurrentMethod(interceptorAttribute);
                    break;
                case InterceptorScope.Class:
                    il.EmitGetAttributeFromClass(method.DeclaringType, interceptorAttribute);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        protected void EmitAttribute(ILProcessor il, MethodDefinition method, FieldReference memberInfoField, TypeReference interceptorAttribute, InterceptorScope scope)
        {
            switch (scope)
            {
                case InterceptorScope.Member:
                    il.EmitGetAttribute(memberInfoField, interceptorAttribute);
                    break;
                case InterceptorScope.Class:
                    il.EmitGetAttributeFromClass(method.DeclaringType, interceptorAttribute);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}