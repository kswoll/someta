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
            Debugger.Launch();

            // We don't want to intercept both async and non-async when the interceptor implements both interfaces
            if (Context.TaskType.IsAssignableFrom(method.ReturnType) && asyncMethodInterceptorInterface.IsAssignableFrom(extensionPoint.AttributeType))
            {
                return;
            }

            LogInfo($"Weaving method interceptor {extensionPoint.AttributeType.FullName} at {method.Describe()}");

            var methodInfoField = method.CacheMethodInfo();
            var attributeField = CacheAttributeInstance(method, methodInfoField, extensionPoint);
            var proceedReference = ImplementProceed(method, extensionPoint, out var proceedStruct, out var proceedStructConstructor);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementBody(method, il, attributeField, methodInfoField, proceedReference, proceedStruct, proceedStructConstructor);
            });
        }

        private void ImplementBody(MethodDefinition method, ILProcessor il, FieldDefinition attributeField, FieldDefinition methodInfoField, MethodReference proceed, TypeDefinition proceedStruct, MethodDefinition proceedStructConstructor)
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
            il.EmitStruct(proceedStruct, proceedStructConstructor, () =>
            {
                il.Emit(OpCodes.Ldarg_0);
            });
            il.Emit(OpCodes.Box, proceedStruct);
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

        private MethodReference ImplementProceed(MethodDefinition method, ExtensionPointAttribute extensionPoint, out TypeDefinition proceedStruct, out MethodDefinition proceedStructConstructor)
        {
            LogInfo($"ImplementProceed: {method.ReturnType}");

            if (method.HasGenericParameters)
            {
                Debugger.Launch();
            }

            var type = method.DeclaringType;//.Import();

            var proceedClassName = GenerateUniqueName(method, extensionPoint.AttributeType, "Proceed");
            proceedStruct = new TypeDefinition(method.DeclaringType.Namespace, proceedClassName, TypeAttributes.NestedPrivate | WeaverContext.Struct, Context.ValueType);
            FieldDefinition instanceField = null;
            if (!method.IsStatic)
            {
                instanceField = new FieldDefinition("$this", FieldAttributes.Private, method.DeclaringType);
                proceedStruct.Fields.Add(instanceField);

                proceedStructConstructor = new MethodDefinition(".ctor", WeaverContext.Constructor, TypeSystem.VoidReference);
                proceedStructConstructor.Parameters.Add(new ParameterDefinition(method.DeclaringType));
                proceedStructConstructor.Body = new MethodBody(proceedStructConstructor);
                proceedStructConstructor.Body.InitLocals = true;
                proceedStructConstructor.Body.Emit(il =>
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Stfld, instanceField);
                    il.Emit(OpCodes.Ret);
                });
                proceedStruct.Methods.Add(proceedStructConstructor);
            }
            else
            {
                proceedStructConstructor = null;
            }

            method.DeclaringType.CopyGenericParameters(proceedStruct);
            method.CopyGenericParameters(proceedStruct);
            method.DeclaringType.NestedTypes.Add(proceedStruct);

            var original = method.MoveImplementation($"{method.Name}$Original");

            var proceed = new MethodDefinition("Proceed", MethodAttributes.Public, TypeSystem.ObjectReference);
            proceed.Parameters.Add(new ParameterDefinition(Context.ObjectArrayType));
            proceedStruct.Methods.Add(proceed);

            MethodReference proceedReference = proceed;
            TypeReference genericProceedType = proceedStruct;
            TypeReference genericType = type;
            if (type.HasGenericParameters || method.HasGenericParameters)
            {
                genericProceedType = proceedStruct.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray());
                genericType = type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray());
                proceedReference = proceed.Bind((GenericInstanceType)genericProceedType);
            }

            proceed.Body.Emit(il =>
            {
                if (!method.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.LoadField(instanceField);                    // Load "this" for when calling "Original"
                    il.Emit(OpCodes.Castclass, genericType);

                    // Load target for subsequent call
//                    il.Emit(OpCodes.Ldarg_0);
//                    il.LoadField(instanceField);                    // Load "this"
//                    il.Emit(OpCodes.Castclass, genericProceedType);
                }

                DecomposeArrayIntoArguments(il, method);

                MethodReference genericProceedTargetMethod = original;//.BindAll(type, proceed);

                if (type.HasGenericParameters)
                {
                    genericProceedTargetMethod = genericProceedTargetMethod.Bind((GenericInstanceType)genericProceedType);
                }

/*                if (method.HasGenericParameters)
                {
                    genericProceedTargetMethod = genericProceedTargetMethod.MakeGenericMethod(proceed.GenericParameters.ToArray());
                }
*/
                il.Emit(OpCodes.Call, genericProceedTargetMethod);

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