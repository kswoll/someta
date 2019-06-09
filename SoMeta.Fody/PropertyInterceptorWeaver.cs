using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class PropertyInterceptorWeaver : BaseWeaver
    {
        private TypeReference propertyInterceptorAttribute;
        private MethodReference baseGetPropertyValue;
        private MethodReference baseSetPropertyValue;

        public PropertyInterceptorWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning, TypeReference propertyInterceptorAttribute) :
            base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
            this.propertyInterceptorAttribute = propertyInterceptorAttribute;
            baseGetPropertyValue = moduleDefinition.FindMethod(propertyInterceptorAttribute, "GetPropertyValue");
            baseSetPropertyValue = moduleDefinition.FindMethod(propertyInterceptorAttribute, "SetPropertyValue");
        }

        public void Weave(PropertyDefinition property, CustomAttribute interceptor)
        {
            var type = property.DeclaringType;
            LogInfo($"Weaving property interceptor {interceptor.AttributeType.FullName} at {type.FullName}.{property.Name}");

            // Check to see if the get interceptor is overridden
            var attributeType = interceptor.AttributeType.Resolve();

            var interceptsGetter = attributeType.IsMethodOverridden(baseGetPropertyValue);
            var interceptsSetter = attributeType.IsMethodOverridden(baseSetPropertyValue);

            if (interceptsGetter || interceptsSetter)
            {
                var propertyInfoField = property.CachePropertyInfo();

                if (interceptsGetter)
                {
                    LogInfo("Getter is intercepted");
                }

                if (interceptsSetter)
                {
                    LogInfo("Setter is intercepted");

                    var proceed = new MethodDefinition($"{property.SetMethod.Name}$Proceed", MethodAttributes.Public, TypeSystem.VoidReference);
                    proceed.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));
                    proceed.Body = new MethodBody(proceed);
                    proceed.Body.InitLocals = true;
                    type.Methods.Add(proceed);

                    MethodReference proceedReference = proceed;
                    var method = property.SetMethod;
                    if (type.HasGenericParameters)
                    {
                        proceedReference = proceed.Bind(type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray()));
                    }

                    var original = MoveMethodImplementation(method, $"{method.Name}$Original");

                    proceed.Body.Emit(il =>
                    {
                        ImplementProceedSet(type, original, il);
                    });

                    // Re-implement method
                    method.Body = new MethodBody(method);
                    method.Body.Emit(il =>
                    {
                        ImplementBody(propertyInfoField, method, il, proceedReference);
                    });
                }
            }
            else
            {
                LogWarning("Interceptor intercepts neither the getter nor the setter");
            }
        }

        private MethodDefinition MoveMethodImplementation(MethodDefinition original, string newName)
        {
            var method = new MethodDefinition(newName, MethodAttributes.Private, original.ReturnType);
            method.CustomAttributes.Add(new CustomAttribute(Context.OriginalMethodAttributeConstructor)
            {
                ConstructorArguments = { new CustomAttributeArgument(TypeSystem.StringReference, method.Name) }
            });
            original.CopyParameters(method);
            original.CopyGenericParameters(method);

            method.DebugInformation.Scope = original.DebugInformation.Scope;
            method.DebugInformation.StateMachineKickOffMethod = original.DebugInformation.StateMachineKickOffMethod;
            foreach (var sequencePoint in original.DebugInformation.SequencePoints)
            {
                method.DebugInformation.SequencePoints.Add(sequencePoint);
            }
            method.Body = new MethodBody(method);
            foreach (var variable in original.Body.Variables)
            {
                method.Body.InitLocals = true;
                method.Body.Variables.Add(new VariableDefinition(variable.VariableType));
            }
            foreach (var handler in original.Body.ExceptionHandlers)
            {
                method.Body.ExceptionHandlers.Add(handler);
            }
            method.Body.Emit(il =>
            {
                foreach (var instruction in original.Body.Instructions)
                {
                    il.Append(instruction);
                }
            });
            original.DeclaringType.Methods.Add(method);

            // Erase scope since the body is being moved into the $Original method
            original.DebugInformation.Scope = null;
            original.DebugInformation.StateMachineKickOffMethod = null;
            original.DebugInformation.SequencePoints.Clear();

            original.Body = new MethodBody(original);

            return method;
        }

        private void ImplementBody(FieldDefinition propertyInfoField, MethodDefinition method, ILProcessor il, MethodReference proceed)
        {
            // We want to call the interceptor's setter method:
            // void SetPropertyValue(PropertyInfo propertyInfo, object instance, object newValue, Action<object> setter)

            // Get interceptor attribute
            il.EmitGetAttribute(propertyInfoField, propertyInterceptorAttribute);

            // Leave PropertyInfo on the stack as the first argument
            var methodSignature = method.GenerateSignature();
            var methodFinder = Context.MethodFinder.MakeGenericInstanceType(method.DeclaringType);
            var findProperty = Context.FindProperty.Bind(methodFinder);
            il.Emit(OpCodes.Ldstr, methodSignature);
            il.Emit(OpCodes.Call, findProperty);

            // Leave instance on the stack as the second argument
            il.Emit(OpCodes.Ldarg_0);

            // Leave the new value on the stack as the third argument
            il.Emit(OpCodes.Ldarg_1);
            il.EmitBoxIfNeeded(method.Parameters[0].ParameterType);

            // Leave the delegate for the proceed implementation on the stack as the fourth argument
            var proceedDelegateType = Context.Action1Type.MakeGenericInstanceType(TypeSystem.ObjectReference);
            var proceedDelegateTypeConstructor = Context.Action1Type.Resolve().GetConstructors().First().Bind(proceedDelegateType);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldftn, proceed);
            il.Emit(OpCodes.Newobj, proceedDelegateTypeConstructor);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, baseSetPropertyValue);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private void ImplementProceedSet(TypeDefinition type, MethodDefinition method, ILProcessor il)
        {
            var parameterInfos = method.Parameters;

            // Load target for subsequent call
            il.Emit(OpCodes.Ldarg_0);                    // Load "this"
            il.Emit(OpCodes.Castclass, method.DeclaringType);

            var parameterInfo = parameterInfos[0];

            // Push object argument (setter.value)
            il.Emit(OpCodes.Ldarg_1);

            // If it's a value type, unbox it
            if (parameterInfo.ParameterType.IsValueType || parameterInfo.ParameterType.IsGenericParameter)
                il.Emit(OpCodes.Unbox_Any, parameterInfo.ParameterType.ResolveGenericParameter(type).Import());
            // Otherwise, cast it
            else
                il.Emit(OpCodes.Castclass, parameterInfo.ParameterType.ResolveGenericParameter(type).Import());

            MethodReference result = method;
            if (type.HasGenericParameters)
                result = method.Bind(type.MakeGenericInstanceType(type.GenericParameters.ToArray()));
            var proceedTargetMethod = result.Import();
            var genericProceedTargetMethod = proceedTargetMethod;
            if (method.GenericParameters.Count > 0)
                genericProceedTargetMethod = genericProceedTargetMethod.MakeGenericMethod(method.GenericParameters.Select(x => x.ResolveGenericParameter(type)).ToArray());

            il.Emit(OpCodes.Callvirt, genericProceedTargetMethod);
            il.Emit(OpCodes.Ret);
        }
    }
}