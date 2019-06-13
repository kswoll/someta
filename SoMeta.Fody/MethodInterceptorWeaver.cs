using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeSystem = Fody.TypeSystem;

namespace Someta.Fody
{
    public class MethodInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference baseInvoke;

        public MethodInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem,
            Action<string> logInfo, Action<string> logError, Action<string> logWarning,
            TypeReference methodInterceptorInterface)
            : base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            baseInvoke = moduleDefinition.FindMethod(methodInterceptorInterface, "Invoke");
        }

        public void Weave(MethodDefinition method, InterceptorAttribute interceptor)
        {
            LogInfo($"Weaving method interceptor {interceptor.AttributeType.FullName} at {method.Describe()}");

            var methodInfoField = method.CacheMethodInfo();
            var attributeField = CacheAttributeInstance(method, methodInfoField, interceptor);
            var proceedReference = ImplementProceed(method);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementBody(method, il, attributeField, methodInfoField, proceedReference);
            });
        }

        private void ImplementBody(MethodDefinition method, ILProcessor il, FieldDefinition attributeField, FieldDefinition methodInfoField, MethodReference proceed)
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
            if (method.HasGenericParameters)
            {
//                Debugger.Launch();
            }

            var type = method.DeclaringType;
            var original = method.MoveImplementation($"{method.Name}$Original");
            var proceed = method.CreateSimilarMethod($"{method.Name}$Proceed", MethodAttributes.Private, TypeSystem.ObjectReference);
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

                DecomposeArrayIntoArguments(il, method);

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