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
        private MethodReference asyncInvokerUnwrap;
        private MethodReference asyncInvokerWrap;

        public AsyncMethodInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference methodInterceptorAttribute, MethodReference asyncInvokerWrap, MethodReference asyncInvokerUnwrap) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            this.methodInterceptorAttribute = methodInterceptorAttribute;
            baseInvoke = moduleDefinition.FindMethod(methodInterceptorAttribute, "InvokeAsync");
            this.asyncInvokerWrap = asyncInvokerWrap;
            this.asyncInvokerUnwrap = asyncInvokerUnwrap;
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
            ComposeArgumentsIntoArray(il, method);

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            il.EmitDelegate(proceed, Context.Func2Type, Context.ObjectArrayType, Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference));

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseInvoke);

            // Before we return, we need to convert the `Task<object>` to `Task<T>`  We use the
            // AsyncInvoker helper so we don't have to build the state machine from scratch.
            var unwrappedReturnType = ((GenericInstanceType)method.ReturnType).GenericArguments[0];
            var typedInvoke = asyncInvokerUnwrap.MakeGenericMethod(unwrappedReturnType);
            il.Emit(OpCodes.Call, typedInvoke);

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
            var taskReturnType = Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference);
            var proceed = method.CreateSimilarMethod($"{method.Name}$Proceed", MethodAttributes.Private, taskReturnType);
            method.CopyGenericParameters(proceed, x => $"{x}_Proceed");

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

                DecomposeArrayIntoArguments(il, method);

                var genericProceedTargetMethod = original.BindAll(type, proceed);
                il.Emit(method.IsStatic ? OpCodes.Call : OpCodes.Callvirt, genericProceedTargetMethod);

                // Before we return, we need to wrap the original `Task<T>` into a `Task<object>`
                var unwrappedReturnType = ((GenericInstanceType)method.ReturnType).GenericArguments[0];
                var typedInvoke = asyncInvokerWrap.MakeGenericMethod(unwrappedReturnType);
                il.Emit(OpCodes.Call, typedInvoke);

                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
    }
}