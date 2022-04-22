using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class AsyncMethodInterceptorWeaver : BaseWeaver
    {
        private MethodReference baseInvoke;
        private MethodReference asyncInvokerUnwrap;
        private MethodReference asyncInvokerWrap;

        public AsyncMethodInterceptorWeaver(WeaverContext context, TypeReference methodInterceptorInterface,
            MethodReference asyncInvokerWrap, MethodReference asyncInvokerUnwrap)
            : base(context)
        {
            baseInvoke = ModuleDefinition.FindMethod(methodInterceptorInterface, "InvokeAsync");
            this.asyncInvokerWrap = asyncInvokerWrap;
            this.asyncInvokerUnwrap = asyncInvokerUnwrap;
        }

        public void Weave(MethodDefinition method, ExtensionPointAttribute extensionPoint)
        {
            if (!Context.TaskType.IsAssignableFrom(method.ReturnType))
            {
                return;
            }

            LogInfo($"Weaving async method interceptor {extensionPoint.AttributeType.FullName} at {method.Describe()}");

            var methodInfoField = method.CacheMethodInfo();
            var attributeField = CacheAttributeInstance(method, methodInfoField, extensionPoint);
            var builder = ImplementProceed(method, extensionPoint);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementBody(method, il, attributeField, methodInfoField, builder);
            });
        }

        private void ImplementBody(MethodDefinition method, ILProcessor il, FieldDefinition attributeField, FieldDefinition methodInfoField, MethodInterceptorBuilder builder)
        {
            // We want to call the interceptor's setter method:
            // Task<object> InvokeMethodAsync(MethodInfo methodInfo, object instance, object[] typeArguments, object[] parameters, Func<object[], Task<object>> invoker)

            // Get interceptor attribute
            il.LoadField(attributeField);

            // Leave MethodInfo on the stack as the first argument
            il.LoadField(methodInfoField);

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Collect all the method type arguments into a single array as the third argument
            ComposeTypeArgumentsIntoArray(il, method);

            // Colllect all the parameters into a single array as the fourth argument
            ComposeArgumentsIntoArray(il, method);

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            builder.EmitProceedStruct(il);
            il.EmitDelegate(builder.ProceedReference, Context.Func2Type, Context.ObjectArrayType, Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference));

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseInvoke);

            // Before we return, we need to convert the `Task<object>` to `Task<T>`  We use the
            // AsyncInvoker helper so we don't have to build the state machine from scratch. Note: this
            // obviously only applies if the return type is Task<T> vs Task.
            if (method.ReturnType is GenericInstanceType)
            {
                var unwrappedReturnType = ((GenericInstanceType)method.ReturnType).GenericArguments[0];
                var typedInvoke = asyncInvokerUnwrap.MakeGenericMethod(unwrappedReturnType);
                il.Emit(OpCodes.Call, typedInvoke);
            }

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodInterceptorBuilder ImplementProceed(MethodDefinition method, ExtensionPointAttribute extensionPoint)
        {
            var builder = new MethodInterceptorBuilder(this, method, extensionPoint);

            var taskReturnType = Context.TaskTType.MakeGenericInstanceType(TypeSystem.ObjectReference);
            var proceed = new MethodDefinition("Proceed", MethodAttributes.Public, taskReturnType);
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));
            builder.Proceed = proceed;
            builder.Build();

            proceed.Body.Emit(il =>
            {
                builder.EmitCallOriginal(il);

                // Before we return, we need to wrap the original `Task<T>` into a `Task<object>` (if it's actually a Task<T> vs. a Task)
                if (method.ReturnType is GenericInstanceType taskT)
                {
                    var unwrappedReturnType = taskT.GenericArguments[0];

                    // If the return type is a generic method parameter, we need to replace the return type with the
                    // type parameter that represents that argument in the class created to house the Proceed method.
                    // i.e. If the original method call was `Task<T> M<T>()` we can't use the type parameter `T` here as it
                    // doesn't exist.  Instead, we need to replace it with the type parmeter in the type representing the
                    // generic method parameter.
                    if (unwrappedReturnType.IsGenericParameter && method.GenericParameters.Contains((GenericParameter)unwrappedReturnType))
                    {
                        unwrappedReturnType = proceed.DeclaringType.GenericParameters.Single(x => x.Name == unwrappedReturnType.Name);
                    }

                    var typedInvoke = asyncInvokerWrap.MakeGenericMethod(unwrappedReturnType);
                    il.Emit(OpCodes.Call, typedInvoke);
                }

                il.Emit(OpCodes.Ret);
            });

            return builder;
        }
    }
}