using System;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class AsyncMethodInterceptorWeaver : BaseWeaver
    {
        private TypeReference methodInterceptorAttribute;
        private MethodReference baseInvoke;
        private MethodReference asyncInvokerInvoke;

        public AsyncMethodInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference methodInterceptorAttribute, MethodReference asyncInvokerInvoke) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            this.methodInterceptorAttribute = methodInterceptorAttribute;
            baseInvoke = moduleDefinition.FindMethod(methodInterceptorAttribute, "InvokeAsync");
            this.asyncInvokerInvoke = asyncInvokerInvoke;
        }

        public void Weave(MethodDefinition method, CustomAttribute interceptor)
        {
            LogInfo($"Weaving async method interceptor {interceptor.AttributeType.FullName} at {method.Describe()}");

            // Check to see if the get interceptor is overridden
            var attributeType = interceptor.AttributeType.Resolve();

            var isIntercepted = attributeType.IsMethodOverridden(baseInvoke);

            if (isIntercepted)
            {
                LogInfo("Method is intercepted");

                var proceedReference = ImplementProceed(method);

                // Re-implement method
                method.Body.Emit(il =>
                {
                    ImplementBody(method, il, proceedReference);
                });
            }
            else
            {
                LogWarning("Interceptor does not override any intercept methods");
            }
        }

        private void ImplementBody(MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
//            Debugger.Launch();

            // We want to call the interceptor's setter method:
            // Task<object> InvokeMethodAsync(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], Task<object>> invoker)

            // Get interceptor attribute
            il.EmitGetAttributeFromCurrentMethod(methodInterceptorAttribute);

            // Leave MethodInfo on the stack as the first argument
            il.LoadCurrentMethodInfo();

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Colllect all the parameters into a single array as the third argument
            il.Emit(OpCodes.Ldc_I4, method.Parameters.Count);       // Array length
            il.Emit(OpCodes.Newarr, TypeSystem.ObjectReference);    // Instantiate array
            int startingIndex = method.IsStatic ? 0 : 1;
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                // Duplicate array
                il.Emit(OpCodes.Dup);

                // Array index
                il.Emit(OpCodes.Ldc_I4, i);

                // Element value
                il.Emit(OpCodes.Ldarg, (short)(i + startingIndex));

                var parameter = method.Parameters[i];
                if (parameter.ParameterType.IsValueType || parameter.ParameterType.IsGenericParameter)
                    il.Emit(OpCodes.Box, parameter.ParameterType.Import());

                // Set array at index to element value
                il.Emit(OpCodes.Stelem_Any, TypeSystem.ObjectReference);
            }

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
//            var taskFunc2Type = Context.Func2Type.MakeGenericInstanceType(Context.ObjectArrayType, Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference));
            il.EmitDelegate(proceed, Context.Func2Type, Context.ObjectArrayType, Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference));

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseInvoke);

            asyncInvokerInvoke.MakeGenericMethod(((GenericInstanceType)method.ReturnType).GenericArguments[0]);
            il.Emit(OpCodes.Call, asyncInvokerInvoke);

            // Now unbox the value if necessary
//            il.EmitUnboxIfNeeded(method.ReturnType, method.DeclaringType);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceed(MethodDefinition method)
        {
            if (method.HasGenericParameters)
            {
//                Debugger.Launch();
            }

            var type = method.DeclaringType;
            var original = method.MoveImplementation($"{method.Name}$Original");
            var taskReturnType = Context.TaskTType.MakeGenericInstanceType(Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference));
            var proceed = method.CreateSimilarMethod($"{method.Name}$Proceed", MethodAttributes.Private, taskReturnType);
            method.CopyGenericParameters(proceed, x => $"{x}_Proceed");

            LogInfo($"ImplementProceed: {method.ReturnType}");
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));

            MethodReference proceedReference = proceed;
            if (method.HasGenericParameters)
            {
                proceedReference = proceedReference.MakeGenericMethod(method.GenericParameters.Select(x => x.ResolveGenericParameter(null)).ToArray());
            }

            proceed.Body.Emit(il =>
            {
                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                }

                // Decompose array into arguments
                for (var i = 0; i < method.Parameters.Count; i++)
                {
                    var parameterInfo = method.Parameters[i];
                    il.Emit(method.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);                                                    // Push array
                    il.Emit(OpCodes.Ldc_I4, i);                                                  // Push element index
                    il.Emit(OpCodes.Ldelem_Any, TypeSystem.ObjectReference);                     // Get element
                    il.EmitUnboxIfNeeded(parameterInfo.ParameterType, type);
                }

                var genericProceedTargetMethod = original.BindAll(type, proceed);
                il.Emit(method.IsStatic ? OpCodes.Call : OpCodes.Callvirt, genericProceedTargetMethod);

                if (method.ReturnType.CompareTo(TypeSystem.VoidReference))
                {
                    // Void methods won't leave anything on the stack, but the proceed method is expected to return a value
                    il.Emit(OpCodes.Ldnull);
                }
                else
                {
                    // If it's a value type, box it
                    il.EmitBoxIfNeeded(method.ReturnType);
                }

                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
    }
}