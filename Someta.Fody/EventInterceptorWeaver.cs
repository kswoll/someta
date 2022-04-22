using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Someta.Fody
{
    public class EventInterceptorWeaver : BaseWeaver
    {
        private readonly MethodReference addEventHandler;
        private readonly MethodReference removeEventHandler;

        public EventInterceptorWeaver(WeaverContext context) : base(context)
        {
            var eventAddInterceptorInterface = FindType("Someta", "IEventAddInterceptor");
            var eventRemoveInterceptorInterface = FindType("Someta", "IEventRemoveInterceptor");
            addEventHandler = ModuleDefinition.FindMethod(eventAddInterceptorInterface, "AddEventHandler");
            removeEventHandler = ModuleDefinition.FindMethod(eventRemoveInterceptorInterface, "RemoveEventHandler");
        }

        public void Weave(EventDefinition @event, ExtensionPointAttribute extensionPoint, bool isAdd)
        {
            var type = @event.DeclaringType;
            LogInfo($"Weaving event add interceptor {extensionPoint.AttributeType.FullName} at {type.FullName}.{@event.Name}");

            var eventInfoField = @event.CacheEventInfo();
            var attributeField = CacheAttributeInstance(@event, eventInfoField, extensionPoint);

            LogInfo("Setter is intercepted");

            var method = isAdd ? @event.AddMethod : @event.RemoveMethod;
            var proceedReference = ImplementProceedSet(method, extensionPoint.AttributeType);

            // Re-implement method
            method.Body.Emit(il =>
            {
                ImplementBody(attributeField, eventInfoField, method, il, proceedReference, isAdd ? addEventHandler : removeEventHandler);
            });
        }

        private void ImplementBody(FieldDefinition attributeField, FieldDefinition eventInfoField,
            MethodDefinition method, ILProcessor il, MethodReference proceed, MethodReference addOrRemoveHandler)
        {
            // We want to call the interceptor's AddEventHandler method:
            // void AddEventHandler(EventInfo eventInfo, object instance, Delegate handler, Action<Delegate> proceed)

            // Get interceptor attribute
            il.LoadField(attributeField);

            // Leave EventInfo on the stack as the first argument
            il.LoadField(eventInfoField);

            // Leave the instance on the stack as the second argument
            EmitInstanceArgument(il, method);

            // Leave the handler on the stack as the third argument
            il.EmitArgument(method, 0);
            il.EmitBoxIfNeeded(method.Parameters[0].ParameterType);

            // Leave the delegate for the proceed implementation on the stack as fourth argument
            il.EmitLocalMethodDelegate(proceed, Context.Action1Type, Context.DelegateType);

            // Finally, we emit the call to the interceptor
            il.Emit(OpCodes.Callvirt, addOrRemoveHandler);

            // Return
            il.Emit(OpCodes.Ret);
        }

        private MethodReference ImplementProceedSet(MethodDefinition method, TypeReference interceptorAttribute)
        {
            var type = method.DeclaringType;
            var original = method.MoveImplementation(GenerateUniqueName(method, interceptorAttribute, "Original"));
            var proceed = method.CreateMethodThatMatchesStaticScope(GenerateUniqueName(method, interceptorAttribute, "Proceed"),
                MethodAttributes.Private, TypeSystem.VoidReference);

            proceed.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));

            MethodReference proceedReference = proceed;
            TypeReference genericType = type;
            if (type.HasGenericParameters)
            {
                genericType = type.MakeGenericInstanceType(type.GenericParameters.Concat(method.GenericParameters).ToArray());
                proceedReference = proceed.Bind((GenericInstanceType)genericType);
            }
            proceed.Body.Emit(il =>
            {
                var parameterInfos = original.Parameters;

                if (!method.IsStatic)
                {
                    // Load target for subsequent call
                    il.Emit(OpCodes.Ldarg_0);                    // Load "this"
                    il.Emit(OpCodes.Castclass, genericType);
                }

                var parameterInfo = parameterInfos[0];

                // Push object argument (setter.value)
                il.EmitArgument(method, 0);

                // If it's a value type, unbox it
                il.EmitUnboxIfNeeded(parameterInfo.ParameterType, type);

                var genericProceedTargetMethod = original.BindAll(type);
                il.EmitCall(genericProceedTargetMethod);
                il.Emit(OpCodes.Ret);
            });

            return proceedReference;
        }
    }
}