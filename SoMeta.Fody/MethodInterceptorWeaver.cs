using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class MethodInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference baseInvoke;
        private readonly TypeReference asyncMethodInterceptorInterface;

        public MethodInterceptorWeaver(WeaverContext context, TypeReference methodInterceptorInterface, TypeReference asyncMethodInterceptorInterface)
            : base(context)
        {
            this.asyncMethodInterceptorInterface = asyncMethodInterceptorInterface;
            baseInvoke = ModuleDefinition.FindMethod(methodInterceptorInterface, "Invoke");
        }

        public void Weave(MethodDefinition method, ExtensionPointAttribute extensionPoint)
        {
//            Debugger.Launch();
            // We don't want to intercept both async and non-async when the interceptor implements both interfaces
            if (Context.TaskType.IsAssignableFrom(method.ReturnType) && asyncMethodInterceptorInterface.IsAssignableFrom(extensionPoint.AttributeType))
            {
                return;
            }

            LogInfo($"Weaving method interceptor {extensionPoint.AttributeType.FullName} at {method.Describe()}");

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
            // object InvokeMethod(MethodInfo methodInfo, object instance, object[] parameters, Func<object[], object> invoker)

            // Get interceptor attribute
            il.LoadField(attributeField);

            // Leave MethodInfo on the stack as the first argument
            il.LoadField(methodInfoField);

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Colllect all the parameters into a single array as the third argument
            ComposeArgumentsIntoArray(il, method);

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            builder.EmitProceedStruct(il);
            il.EmitDelegate(builder.ProceedReference, Context.Func2Type, Context.ObjectArrayType, TypeSystem.ObjectReference);

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

        private MethodInterceptorBuilder ImplementProceed(MethodDefinition method, ExtensionPointAttribute extensionPoint)
        {
//            Debugger.Launch();

            LogInfo($"ImplementProceed: {method.ReturnType}");

            if (method.HasGenericParameters)
            {
//                Debugger.Launch();
            }

            var builder = new MethodInterceptorBuilder(this, method, extensionPoint);

            var proceed = new MethodDefinition("Proceed", MethodAttributes.Public, TypeSystem.ObjectReference);
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));
            builder.Proceed = proceed;
            builder.Build();

            proceed.Body.Emit(il =>
            {
                builder.EmitProceedInstance(il);
                builder.DecomposeArrayIntoArguments(il);
                builder.EmitCallOriginal(il);

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

            return builder;
        }
    }
}