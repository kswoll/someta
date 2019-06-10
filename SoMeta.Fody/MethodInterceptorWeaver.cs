using System;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class MethodInterceptorWeaver : BaseWeaver
    {
        private TypeReference methodInterceptorAttribute;
        private MethodReference baseInvoke;
//        private MethodReference baseInvokeAsync;

        public MethodInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference methodInterceptorAttribute) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            this.methodInterceptorAttribute = methodInterceptorAttribute;
            baseInvoke = moduleDefinition.FindMethod(methodInterceptorAttribute, "InvokeMethod");
        }

        public void Weave(MethodDefinition method, CustomAttribute interceptor)
        {
            LogInfo($"Weaving method interceptor {interceptor.AttributeType.FullName} at {method.Describe()}");

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
            // We want to call the interceptor's setter method:
            // void GetPropertyValue(PropertyInfo propertyInfo, object instance, Action<object> getter)

            // Get interceptor attribute
            il.EmitGetAttributeFromCurrentMethod(methodInterceptorAttribute);

            // Leave PropertyInfo on the stack as the first argument
            il.LoadCurrentMethodInfo();

            if (!method.IsStatic)
            {
                // Leave instance (this) on the stack as the second argument
                il.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

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
            il.EmitDelegate(proceed, Context.Func2Type, Context.ObjectArrayType, TypeSystem.ObjectReference);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseInvoke);

            if (method.ReturnType.CompareTo(TypeSystem.VoidReference))
            {
                il.Emit(OpCodes.Pop);
            }
            else
            {
                // Now unbox the value if necessary
                il.EmitUnboxIfNeeded(method.ReturnType, method.DeclaringType);
            }

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceed(MethodDefinition method)
        {
            if (method.DeclaringType.HasGenericParameters)
            {
//                Debugger.Launch();
            }

            var type = method.DeclaringType;
//            var original = method.MoveImplementation($"{method.Name}$Original");
            var proceed = method.CreateSimilarMethod($"{method.Name}$Proceed", MethodAttributes.Private, TypeSystem.ObjectReference);
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));
            proceed.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ret);
            });
            return proceed;

            method.CopyGenericParameters(proceed);

            LogInfo($"ImplementProceed: {method.ReturnType}");
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));

            MethodReference proceedReference = proceed;
            if (type.HasGenericParameters)
            {
//                proceedReference = proceed.Bind(type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray()));
//                if (method.HasGenericParameters)
//                    proceedReference = proceedReference.MakeGenericMethod(method.GenericParameters.ToArray());
            }

/*            if (method.HasGenericParameters)
            {
                proceedReference = proceedReference.MakeGenericMethod(method.GenericParameters.Select(x => x.ResolveGenericParameter(null)).ToArray());
            }
*/
/*            proceed.Body.Emit(il =>
            {
                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
//                    il.Emit(OpCodes.Castclass, original.DeclaringType);
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

                var genericProceedTargetMethod = original.BindAll(type);
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
*/        }
    }
}