using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using TypeSystem = Fody.TypeSystem;

namespace SoMeta.Fody
{
    public class StateWeaver : BaseWeaver
    {
        private readonly TypeReference injectedFieldType;
        private readonly TypeReference injectFieldAttributeType;

        public StateWeaver(ModuleDefinition moduleDefinition, WeaverContext context, TypeSystem typeSystem, Action<string> logInfo, Action<string> logError, Action<string> logWarning)
            : base(moduleDefinition, context, typeSystem, logInfo, logError, logWarning)
        {
//            Debugger.Launch();
            injectedFieldType = moduleDefinition.FindType("SoMeta", "InjectedField`1", Context.SoMeta, "T");
            injectFieldAttributeType = moduleDefinition.FindType("SoMeta", "InjectFieldAttribute", Context.SoMeta);
        }

        public void Weave(IMemberDefinition member, InterceptorAttribute interceptor)
        {
            var type = member is TypeDefinition definition ? definition : member.DeclaringType;

            FieldDefinition attributeField;
            if (member is TypeDefinition)
            {
                attributeField = CacheAttributeInstance(type, interceptor);
            }
            else if (member is PropertyDefinition propertyDefinition)
            {
                var propertyInfo = propertyDefinition.CachePropertyInfo();
                attributeField = CacheAttributeInstance(member, propertyInfo, interceptor);
            }
            else if (member is MethodDefinition methodDefinition)
            {
//                Debugger.Launch();
                var methodInfo = methodDefinition.CacheMethodInfo();
                attributeField = CacheAttributeInstance(member, methodInfo, interceptor);
            }
            else
            {
                throw new Exception();
            }

            var attributeType = interceptor.AttributeType.Resolve();
            foreach (var property in attributeType.Properties)
            {
                if (property.PropertyType.IsGenericInstance && property.PropertyType.GetElementType().CompareTo(injectedFieldType))
                {
                    var propertyType = ((GenericInstanceType)property.PropertyType).GenericArguments[0];
                    var fieldName = GenerateUniqueName(member, attributeType, property.Name);
                    var injectFieldAttribute = property.GetCustomAttributes(injectFieldAttributeType).SingleOrDefault();
                    var isStatic = injectFieldAttribute != null && (bool)injectFieldAttribute.ConstructorArguments[0].Value;
                    var fieldAttributes = FieldAttributes.Private;
                    if (isStatic)
                        fieldAttributes |= FieldAttributes.Static;
                    var field = new FieldDefinition(fieldName, fieldAttributes, propertyType);
                    type.Fields.Add(field);

                    // Generate accessors
                    var getMethodName = GenerateUniqueName(member, attributeType, $"{property.Name}Getter");
                    var getMethod = new MethodDefinition(getMethodName, MethodAttributes.Private | MethodAttributes.Static, propertyType);
                    getMethod.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));
                    getMethod.Body = new MethodBody(getMethod);
                    getMethod.Body.InitLocals = true;
                    getMethod.Body.Emit(il =>
                    {
                        if (!isStatic)
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Castclass, type);
                        }
                        il.LoadField(field);
                        il.Emit(OpCodes.Ret);
                    });
                    type.Methods.Add(getMethod);

                    var setMethodName = GenerateUniqueName(member, attributeType, $"{property.Name}Setter");
                    var setMethod = new MethodDefinition(setMethodName, MethodAttributes.Private | MethodAttributes.Static, TypeSystem.VoidReference);
                    setMethod.Parameters.Add(new ParameterDefinition(TypeSystem.ObjectReference));
                    setMethod.Parameters.Add(new ParameterDefinition(propertyType));
                    setMethod.Body = new MethodBody(setMethod);
                    setMethod.Body.InitLocals = true;
                    setMethod.Body.Emit(il =>
                    {
                        if (!isStatic)
                        {
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Castclass, type);
                        }
                        il.Emit(OpCodes.Ldarg_1);
                        il.SaveField(field);
                        il.Emit(OpCodes.Ret);
                    });
                    type.Methods.Add(setMethod);

                    type.EmitStaticConstructor(il =>
                    {
                        var genericInjectedFieldType = injectedFieldType.MakeGenericInstanceType(propertyType);
                        var injectedFieldConstructor = ModuleDefinition.FindConstructor(genericInjectedFieldType).Bind(genericInjectedFieldType);

                        il.LoadField(attributeField);       // Instance on which to set the InjectedField

                        // Instantiate the InjectedField
                        il.EmitDelegate(getMethod, Context.Func2Type, TypeSystem.ObjectReference, propertyType);
                        il.EmitDelegate(setMethod, Context.ActionTypes[2], TypeSystem.ObjectReference, propertyType);
                        il.Emit(OpCodes.Newobj, injectedFieldConstructor);

                        il.EmitCall(property.SetMethod);
                    });
                }
            }
        }
    }
}